import { inject, Injectable, Optional, Inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';

import { trace, Span, SpanStatusCode, context } from '@opentelemetry/api';
import { WebTracerProvider } from '@opentelemetry/sdk-trace-web';
import { OTLPTraceExporter } from '@opentelemetry/exporter-trace-otlp-http';
import { BatchSpanProcessor } from '@opentelemetry/sdk-trace-base';

import { environment } from '@env/environment';
import {
  observeWebVitals,
  getWebVitalRating,
  WebVitalsCallback,
} from './web-vitals';

/**
 * Mapping of Web Vital metric names to their OpenTelemetry convention.
 * Prefix matches the OpenTelemetry RUM semantic convention:
 *   - browser.measurement.lcp
 *   - browser.measurement.inp
 *   - browser.measurement.cls
 *   - browser.measurement.fcp
 *   - browser.measurement.ttfb
 */
const METRIC_TO_OTEL_NAME: Record<string, string> = {
  LCP: 'browser.measurement.lcp',
  INP: 'browser.measurement.inp',
  CLS: 'browser.measurement.cls',
  FCP: 'browser.measurement.fcp',
  TTFB: 'browser.measurement.ttfb',
};

/**
 * Service that initialises Real User Monitoring (RUM) for the application.
 *
 * Responsibilities:
 * 1. Bootstraps the OpenTelemetry Web SDK (`WebTracerProvider`) with an
 *    OTLP HTTP exporter sending spans to the configured OpenTelemetry
 *    Collector endpoint.
 * 2. Collects Core Web Vitals (LCP, INP, CLS, FCP, TTFB) via the
 *    `web-vitals` library and reports each as a dedicated span with
 *    `value`, `rating`, `delta`, and `id` attributes.
 * 3. Captures unhandled JavaScript errors and unhandled promise rejections
 *    as error spans.
 *
 * @example
 * ```ts
 * // Call once during app bootstrap (e.g. in AppComponent.ngOnInit or AppModule):
 * constructor(private rum: RumService) {}
 * ngOnInit(): void {
 *   this.rum.initialize();
 * }
 * ```
 */
@Injectable({ providedIn: 'root' })
export class RumService {
  /** The OpenTelemetry tracer instance used to create custom spans. */
  private tracer: ReturnType<typeof trace.getTracer> | null = null;

  /** Whether the service has been initialised (prevents double init). */
  private initialized = false;

  private document = inject(DOCUMENT, { optional: true }) as Document;

  /**
   * Initialises the RUM pipeline.
   *
   * Must be called **once** during application startup. Safe to call
   * multiple times — subsequent invocations are no-ops.
   */
  initialize(): void {
    if (this.initialized || environment.production === undefined) {
      return;
    }
    this.initialized = true;

    // Short-circuit in environments where OTEL collector is not configured.
    if (!environment.otelCollectorUrl) {
      console.warn('[RumService] No OTEL collector URL configured — skipping OpenTelemetry initialisation.');
      return;
    }

    // ── 1. Initialise the WebTracerProvider with OTLP export ────────────
    const exporter = new OTLPTraceExporter({
      url: environment.otelCollectorUrl,
    });

    const provider = new WebTracerProvider({
      spanProcessors: [new BatchSpanProcessor(exporter)],
    });
    provider.register();

    this.tracer = trace.getTracer('his-hope-app', '0.1.0');

    // ── 2. Start observing Web Vitals ──────────────────────────────────
    this.startWebVitalsObservation();

    // ── 3. Capture unhandled JS errors as spans ────────────────────────
    this.captureUnhandledErrors();

    console.log('[RumService] Real User Monitoring initialised.');
  }

  // ────────────────────────────────────────────────────────────────────
  //  Public helpers
  // ────────────────────────────────────────────────────────────────────

  /**
   * Create an arbitrary custom span. Useful for measuring operations
   * outside of the automatic Web Vitals / error collection.
   *
   * @param name    Span name (e.g. `'app.search.submit'`)
   * @param fn      Synchronous or Promise-returning function to wrap
   * @param attrs   Optional attributes to attach to the span
   */
  traceSpan<T>(
    name: string,
    fn: (span: Span) => T,
    attrs: Record<string, string | number | boolean> = {},
  ): T {
    const tracer = this.tracer ?? trace.getTracer('his-hope-app-fallback');
    const span = tracer.startSpan(name);
    span.setAttributes(attrs);

    try {
      const result = fn(span);
      if (result instanceof Promise) {
        return result
          .then((val) => {
            span.setStatus({ code: SpanStatusCode.OK });
            span.end();
            return val;
          })
          .catch((err) => {
            span.recordException(err as Error);
            span.setStatus({ code: SpanStatusCode.ERROR });
            span.end();
            throw err;
          }) as T;
      }

      span.setStatus({ code: SpanStatusCode.OK });
      span.end();
      return result;
    } catch (err) {
      span.recordException(err as Error);
      span.setStatus({ code: SpanStatusCode.ERROR });
      span.end();
      throw err;
    }
  }

  // ────────────────────────────────────────────────────────────────────
  //  Private helpers
  // ────────────────────────────────────────────────────────────────────

  /**
   * Subscribes to all five Web Vitals and emits each as a span.
   */
  private startWebVitalsObservation(): void {
    const handler: WebVitalsCallback = (metric) => {
      this.reportWebVitalAsSpan(metric);
    };
    observeWebVitals(handler);
  }

  /**
   * Converts a single Web Vital report into an OpenTelemetry span with
   * attributes that mirror the metric payload.
   */
  private reportWebVitalAsSpan(metric: import('web-vitals').Metric): void {
    if (!this.tracer) {
      return;
    }

    const spanName = METRIC_TO_OTEL_NAME[metric.name] ?? `browser.measurement.${metric.name.toLowerCase()}`;
    const rating = metric.rating ?? getWebVitalRating(metric);

    const span = this.tracer.startSpan(spanName);
    span.setAttribute('metric.name', metric.name);
    span.setAttribute('metric.value', metric.value);
    span.setAttribute('metric.rating', rating);
    span.setAttribute('metric.delta', metric.delta);
    span.setAttribute('metric.id', metric.id);
    span.setAttribute('metric.navigationType', metric.navigationType ?? '');

    // Map value into a human-friendly label for downstream dashboards.
    span.setAttribute('metric.label', this.getMetricLabel(metric.name));

    span.setStatus({ code: SpanStatusCode.OK });
    span.end();
  }

  /**
   * Registers global error / rejection listeners to capture JS exceptions
   * as OpenTelemetry spans.
   */
  private captureUnhandledErrors(): void {
    // ── window.onerror ───────────────────────────────────────────────
    if (this.document?.defaultView) {
      const win = this.document.defaultView;

      win.addEventListener('error', (event: ErrorEvent) => {
        this.reportErrorAsSpan('unhandled.error', {
          message: event.message ?? 'Unknown error',
          filename: event.filename ?? '',
          lineno: event.lineno ?? 0,
          colno: event.colno ?? 0,
          error: event.error,
        });
      });

      // ── window.onunhandledrejection ─────────────────────────────────
      win.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => {
        const reason = event.reason;
        this.reportErrorAsSpan('unhandled.promise_rejection', {
          message: reason?.message ?? String(reason) ?? 'Unhandled Promise rejection',
          filename: '',
          lineno: 0,
          colno: 0,
          error: reason instanceof Error ? reason : undefined,
        });
      });
    }
  }

  /**
   * Creates an error span from a captured exception payload.
   */
  private reportErrorAsSpan(
    spanName: string,
    details: {
      message: string;
      filename: string;
      lineno: number;
      colno: number;
      error: unknown;
    },
  ): void {
    if (!this.tracer) {
      return;
    }

    const span = this.tracer.startSpan(spanName);
    span.setAttribute('error.message', details.message);
    span.setAttribute('error.source', details.filename);

    if (details.lineno) {
      span.setAttribute('error.lineno', details.lineno);
    }
    if (details.colno) {
      span.setAttribute('error.colno', details.colno);
    }

    if (details.error instanceof Error) {
      span.recordException(details.error);
    }

    span.setStatus({ code: SpanStatusCode.ERROR, message: details.message });
    span.end();
  }

  /**
   * Returns the Vietnamese label for a given metric name.
   * Falls back to the metric name if no label is registered.
   */
  private getMetricLabel(metricName: string): string {
    const labels: Record<string, string> = {
      LCP: 'Thời gian hiển thị nội dung lớn nhất',
      INP: 'Tương tác với nội dung',
      CLS: 'Dịch chuyển bố cục tích lũy',
      FCP: 'Thời gian hiển thị nội dung đầu tiên',
      TTFB: 'Thời gian phản hồi máy chủ',
    };
    return labels[metricName] ?? metricName;
  }
}
