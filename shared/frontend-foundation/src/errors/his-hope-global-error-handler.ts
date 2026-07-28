import { ErrorHandler, Injectable, inject } from "@angular/core";
import { HisHopeErrorReportingService } from "../http/his-hope-error-reporting.service";
import { HisHopeToastService } from "../ui/his-hope-toast.service";

/** Catches otherwise-uncaught errors (template/render errors, rejected
 *  promises Angular surfaces, RxJS subscriptions without an error handler)
 *  so they are reported and the user sees something better than a blank
 *  screen. Register with `{ provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler }`. */
@Injectable({ providedIn: "root" })
export class HisHopeGlobalErrorHandler implements ErrorHandler {
  private readonly errorReporting = inject(HisHopeErrorReportingService);
  private readonly toast = inject(HisHopeToastService);

  handleError(error: unknown): void {
    const message = this.describe(error);
    // eslint-disable-next-line no-console -- last resort visibility when no reporter is configured
    console.error("[His.Hope] Unhandled error:", error);
    this.errorReporting.report({
      message,
      severity: "fatal",
      stack: error instanceof Error ? error.stack : undefined,
    });
    this.toast.error("Something went wrong. Please try again.", {
      detail: message,
    });
  }

  private describe(error: unknown): string {
    if (error instanceof Error) return error.message;
    if (typeof error === "string") return error;
    try {
      return JSON.stringify(error);
    } catch {
      return "Unknown error";
    }
  }
}
