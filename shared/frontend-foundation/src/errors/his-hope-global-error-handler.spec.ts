import { TestBed } from "@angular/core/testing";
import { HisHopeGlobalErrorHandler } from "./his-hope-global-error-handler";
import { HisHopeErrorReportingService } from "../http/his-hope-error-reporting.service";
import { HisHopeToastService } from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

describe("HisHopeGlobalErrorHandler", () => {
  let handler: HisHopeGlobalErrorHandler;
  let errorReporting: HisHopeErrorReportingService;
  let toast: HisHopeToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    handler = TestBed.inject(HisHopeGlobalErrorHandler);
    errorReporting = TestBed.inject(HisHopeErrorReportingService);
    toast = TestBed.inject(HisHopeToastService);
    spyOn(console, "error");
  });

  it("reports a fatal event carrying the error message and stack", () => {
    const error = new Error("boom");
    handler.handleError(error);
    const [event] = errorReporting.events();
    expect(event.message).toBe("boom");
    expect(event.severity).toBe("fatal");
    expect(event.stack).toBe(error.stack);
  });

  it("surfaces a toast so the user is not left with a blank screen", () => {
    handler.handleError(new Error("boom"));
    const [shown] = toast.toasts();
    expect(shown.tone).toBe("error");
  });

  it("describes a plain string error without needing an Error instance", () => {
    handler.handleError("plain string error");
    expect(errorReporting.events()[0].message).toBe("plain string error");
  });

  it("falls back to a generic description for unserializable values", () => {
    const circular: Record<string, unknown> = {};
    circular["self"] = circular;
    handler.handleError(circular);
    expect(errorReporting.events()[0].message).toBe(
      TestBed.inject(HisHopeI18nService).t("errors.unknownError"),
    );
  });
});
