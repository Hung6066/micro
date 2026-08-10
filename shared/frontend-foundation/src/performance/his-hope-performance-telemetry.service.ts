import { Injectable, signal } from "@angular/core";

export interface HisHopePerformanceMetric {
  name: string;
  duration: number;
  startTime: number;
}
export type HisHopePerformanceReporter = (
  metric: HisHopePerformanceMetric,
) => void;

@Injectable({ providedIn: "root" })
export class HisHopePerformanceTelemetryService {
  private readonly metricState = signal<HisHopePerformanceMetric[]>([]);
  private reporter?: HisHopePerformanceReporter;
  readonly metrics = this.metricState.asReadonly();

  configure(reporter: HisHopePerformanceReporter): void {
    this.reporter = reporter;
  }

  clear(): void {
    this.metricState.set([]);
  }

  mark(name: string): void {
    if (typeof performance !== "undefined") performance.mark(name);
  }

  measure(
    name: string,
    startMark: string,
    endMark?: string,
  ): HisHopePerformanceMetric | null {
    if (typeof performance === "undefined") return null;
    const end = endMark ?? `${name}:end`;
    performance.mark(end);
    let entry: PerformanceMeasure;
    try {
      entry = performance.measure(name, startMark, end);
    } catch {
      return null;
    }
    const metric = {
      name,
      duration: entry.duration,
      startTime: entry.startTime,
    };
    this.metricState.update((metrics) => [...metrics.slice(-99), metric]);
    this.reporter?.(metric);
    return metric;
  }

  record(name: string, duration: number): void {
    const metric = {
      name,
      duration,
      startTime: typeof performance === "undefined" ? 0 : performance.now(),
    };
    this.metricState.update((metrics) => [...metrics.slice(-99), metric]);
    this.reporter?.(metric);
  }

  /** Captures Core Web Vitals (LCP, CLS, INP-proxy via first-input) with the
   *  native `PerformanceObserver` \u2014 no `web-vitals` runtime dependency needed.
   *  Safe to call once per app bootstrap; no-ops where unsupported (SSR, older browsers). */
  observeWebVitals(): void {
    if (typeof PerformanceObserver === "undefined") return;

    this.observeEntryType("largest-contentful-paint", (entry) =>
      this.record("LCP", entry.startTime),
    );
    this.observeEntryType("first-input", (entry) => {
      const firstInput = entry as PerformanceEventTiming;
      this.record("INP", firstInput.processingStart - firstInput.startTime);
    });

    let clsValue = 0;
    this.observeEntryType("layout-shift", (entry) => {
      const layoutShift = entry as PerformanceEntry & {
        value: number;
        hadRecentInput: boolean;
      };
      if (layoutShift.hadRecentInput) return;
      clsValue += layoutShift.value;
      this.record("CLS", clsValue);
    });
  }

  private observeEntryType(
    type: string,
    onEntry: (entry: PerformanceEntry) => void,
  ): void {
    try {
      const observer = new PerformanceObserver((list) =>
        list.getEntries().forEach(onEntry),
      );
      observer.observe({ type, buffered: true });
    } catch {
      // Entry type unsupported by this browser \u2014 skip silently.
    }
  }
}
