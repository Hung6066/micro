import { Injectable, signal } from "@angular/core";

export type HisHopeErrorSeverity = "fatal" | "error" | "warning";

export interface HisHopeErrorEvent {
  message: string;
  severity: HisHopeErrorSeverity;
  correlationId?: string;
  statusCode?: number;
  url?: string;
  stack?: string;
  context?: Record<string, unknown>;
}

export type HisHopeErrorReporter = (event: HisHopeErrorEvent) => void;

/** Framework-agnostic sink for uncaught errors and terminal HTTP failures.
 *  Apps wire a real backend (Sentry, Application Insights, a log endpoint...)
 *  via `configure()`; without one, events are kept in memory for debugging. */
@Injectable({ providedIn: "root" })
export class HisHopeErrorReportingService {
  private readonly eventState = signal<HisHopeErrorEvent[]>([]);
  private reporter?: HisHopeErrorReporter;
  readonly events = this.eventState.asReadonly();

  configure(reporter: HisHopeErrorReporter): void {
    this.reporter = reporter;
  }

  report(event: HisHopeErrorEvent): void {
    this.eventState.update((events) => [...events.slice(-49), event]);
    this.reporter?.(event);
  }

  clear(): void {
    this.eventState.set([]);
  }
}
