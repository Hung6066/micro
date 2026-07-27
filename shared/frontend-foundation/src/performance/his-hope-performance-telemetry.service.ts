import { Injectable, signal } from '@angular/core';

export interface HisHopePerformanceMetric { name: string; duration: number; startTime: number; }
export type HisHopePerformanceReporter = (metric: HisHopePerformanceMetric) => void;

@Injectable({ providedIn: 'root' })
export class HisHopePerformanceTelemetryService {
  private readonly metricState = signal<HisHopePerformanceMetric[]>([]);
  private reporter?: HisHopePerformanceReporter;
  readonly metrics = this.metricState.asReadonly();

  configure(reporter: HisHopePerformanceReporter): void { this.reporter = reporter; }

  clear(): void { this.metricState.set([]); }

  mark(name: string): void { if (typeof performance !== 'undefined') performance.mark(name); }

  measure(name: string, startMark: string, endMark?: string): HisHopePerformanceMetric | null {
    if (typeof performance === 'undefined') return null;
    const end = endMark ?? `${name}:end`;
    performance.mark(end);
    let entry: PerformanceMeasure;
    try {
      entry = performance.measure(name, startMark, end);
    } catch {
      return null;
    }
    const metric = { name, duration: entry.duration, startTime: entry.startTime };
    this.metricState.update(metrics => [...metrics.slice(-99), metric]);
    this.reporter?.(metric);
    return metric;
  }

  record(name: string, duration: number): void {
    const metric = { name, duration, startTime: typeof performance === 'undefined' ? 0 : performance.now() };
    this.metricState.update(metrics => [...metrics.slice(-99), metric]);
    this.reporter?.(metric);
  }
}
