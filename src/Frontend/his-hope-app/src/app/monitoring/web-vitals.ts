import {
  onLCP,
  onINP,
  onCLS,
  onFCP,
  onTTFB,
  Metric,
} from 'web-vitals';

/** Re-export the Metric type for consumers. */
export type { Metric as WebVitalMetric };

/**
 * Callback function type for receiving Web Vital metric reports.
 * Each metric contains `name`, `value`, `rating`, `delta`, `id`, and `navigationType`.
 */
export type WebVitalsCallback = (metric: Metric) => void;

/**
 * Mapping of metric names to human-readable Vietnamese labels.
 * Used for display / debugging within the application.
 */
export const WEB_VITAL_LABELS: Record<string, string> = {
  LCP: 'Thời gian hiển thị nội dung lớn nhất',
  INP: 'Tương tác với nội dung',
  CLS: 'Dịch chuyển bố cục tích lũy',
  FCP: 'Thời gian hiển thị nội dung đầu tiên',
  TTFB: 'Thời gian phản hồi máy chủ',
};

/**
 * Rating thresholds used for classifying Web Vital performance.
 * Based on Google's Core Web Vitals guidelines.
 */
export const WEB_VITAL_THRESHOLDS: Record<string, { good: number; needsImprovement: number }> = {
  LCP: { good: 2500, needsImprovement: 4000 },
  INP: { good: 200, needsImprovement: 500 },
  CLS: { good: 0.1, needsImprovement: 0.25 },
  FCP: { good: 1800, needsImprovement: 3000 },
  TTFB: { good: 800, needsImprovement: 1800 },
};

/**
 * Determines the rating string ('good', 'needs-improvement', or 'poor')
 * for a given Web Vital metric based on Google thresholds.
 */
export function getWebVitalRating(metric: Metric): 'good' | 'needs-improvement' | 'poor' {
  const thresholds = WEB_VITAL_THRESHOLDS[metric.name];
  if (!thresholds) {
    return 'needs-improvement';
  }
  if (metric.value <= thresholds.good) {
    return 'good';
  }
  if (metric.value <= thresholds.needsImprovement) {
    return 'needs-improvement';
  }
  return 'poor';
}

/**
 * Initializes observation of all Web Vitals metrics.
 *
 * Registers the `onLCP`, `onINP`, `onCLS`, `onFCP`, and `onTTFB` observers
 * from the `web-vitals` library and forwards every report to the provided
 * callback.
 *
 * @param callback - Function invoked **once per metric per page session** when
 *                   the browser finalises the measurement (or its latest
 *                   possible value).
 *
 * @example
 * ```ts
 * observeWebVitals((metric) => {
 *   console.log(metric.name, metric.value, metric.rating);
 * });
 * ```
 */
export function observeWebVitals(callback: WebVitalsCallback): void {
  // Each metric is reported as soon as the browser produces a stable value.
  // The `web-vitals` library handles reporting once per session per metric.
  onLCP(callback);
  onINP(callback);
  onCLS(callback);
  onFCP(callback);
  onTTFB(callback);
}
