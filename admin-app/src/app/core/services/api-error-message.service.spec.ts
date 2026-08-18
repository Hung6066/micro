import { HttpErrorResponse, HttpHeaders } from "@angular/common/http";
import { TestBed } from "@angular/core/testing";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { ApiErrorMessageService } from "./api-error-message.service";

describe("ApiErrorMessageService", () => {
  let service: ApiErrorMessageService;
  const translations: Record<string, string> = {
    "errors.api.noPreviousPolicy": "No previous policy.",
    "errors.conflict": "Conflict.",
    "errors.networkError": "Network error.",
    "errors.unexpectedError": "Unexpected error.",
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ApiErrorMessageService,
        {
          provide: HisHopeI18nService,
          useValue: {
            t: (key: string, fallback: string) => translations[key] ?? fallback,
          },
        },
      ],
    });
    service = TestBed.inject(ApiErrorMessageService);
  });

  it("translates a stable domain error code instead of exposing server detail", () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: {
        errorCode: "no_previous_policy",
        detail: "internal database detail",
      },
      headers: new HttpHeaders({ "x-correlation-id": "corr-1" }),
    });

    expect(service.normalize(error)).toEqual({
      status: 409,
      code: "no_previous_policy",
      correlationId: "corr-1",
    });
    expect(service.message(error, "errors.unexpectedError")).toBe(
      "No previous policy.",
    );
  });

  it("uses a translated HTTP fallback for unknown error codes", () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { errorCode: "unknown_domain_error", detail: "do not show this" },
    });

    expect(service.message(error)).toBe("Conflict.");
  });

  it("uses the network translation when the request has no HTTP response", () => {
    expect(service.message(new Error("socket secret"))).toBe("Network error.");
  });

  it("keeps legacy machine-readable error strings translatable during migration", () => {
    const error = new HttpErrorResponse({
      status: 404,
      error: "scope_not_found",
    });

    expect(service.normalize(error).code).toBe("scope_not_found");
  });
});
