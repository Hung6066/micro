import { ErrorHandler, Injectable, inject } from "@angular/core";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { HisHopeErrorReportingService } from "../http/his-hope-error-reporting.service";
import { HisHopeToastService } from "@his-hope/frontend-foundation/ui";

/** Catches otherwise-uncaught errors (template/render errors, rejected
 *  promises Angular surfaces, RxJS subscriptions without an error handler)
 *  so they are reported and the user sees something better than a blank
 *  screen. Register with `{ provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler }`. */
@Injectable({ providedIn: "root" })
export class HisHopeGlobalErrorHandler implements ErrorHandler {
  private readonly errorReporting = inject(HisHopeErrorReportingService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);

  handleError(error: unknown): void {
    const message = this.describe(error);
    console.error("[His.Hope] Unhandled error:", error);
    this.errorReporting.report({
      message,
      severity: "fatal",
      stack: error instanceof Error ? error.stack : undefined,
    });
    this.toast.error(this.i18n.t("errors.unhandled"), {
      detail: message,
    });
  }

  private describe(error: unknown): string {
    if (error instanceof Error) return error.message;
    if (typeof error === "string") return error;
    try {
      return JSON.stringify(error);
    } catch {
      return this.i18n.t("errors.unknownError");
    }
  }
}
