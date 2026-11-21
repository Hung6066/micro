import { HttpErrorResponse, HttpHeaders } from "@angular/common/http";
import { TestBed } from "@angular/core/testing";
import { HisHopeI18nService } from "./his-hope-i18n.service";
import { HisHopeApiErrorMessageService } from "./his-hope-api-error-message.service";

describe("HisHopeApiErrorMessageService", () => {
  let service: HisHopeApiErrorMessageService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [HisHopeI18nService, HisHopeApiErrorMessageService],
    });
    service = TestBed.inject(HisHopeApiErrorMessageService);
  });

  it("normalizes shared ProblemDetails and correlation metadata", () => {
    const error = new HttpErrorResponse({
      status: 403,
      error: { errorCode: "facility_scope_denied" },
      headers: new HttpHeaders({ "x-correlation-id": "corr-123" }),
    });

    expect(service.normalize(error)).toEqual({
      status: 403,
      code: "facility_scope_denied",
      correlationId: "corr-123",
    });
  });

  it("supports legacy error payloads and localized status fallback", () => {
    const error = new HttpErrorResponse({
      status: 429,
      error: { error: "rate_limited" },
    });

    expect(service.normalize(error).code).toBe("rate_limited");
    expect(service.message(error)).toBe("Có quá nhiều yêu cầu. Vui lòng chờ rồi thử lại.");
  });
});
