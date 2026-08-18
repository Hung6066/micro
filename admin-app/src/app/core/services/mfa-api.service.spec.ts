import { provideHttpClient } from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { environment } from "../../../environments/environment";
import { MfaApiService } from "./mfa-api.service";

describe("MfaApiService", () => {
  let service: MfaApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        MfaApiService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(MfaApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads MFA status", () => {
    service
      .getStatus()
      .subscribe((status) => expect(status.enabled).toBeTrue());

    const request = http.expectOne(`${environment.authApiUrl}/mfa/status`);
    expect(request.request.method).toBe("GET");
    request.flush({ enabled: true, recoveryCodesRemaining: 4 });
  });

  it("starts MFA enrollment", () => {
    service
      .enroll()
      .subscribe((enrollment) => expect(enrollment.secretKey).toBe("secret"));

    const request = http.expectOne(`${environment.authApiUrl}/mfa/enroll`);
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual({});
    request.flush({
      secretKey: "secret",
      qrCodeUri: "otpauth://totp/test",
      recoveryCodes: ["code-1"],
    });
  });

  it("verifies the submitted MFA code", () => {
    service.verify("123456").subscribe();

    const request = http.expectOne(`${environment.authApiUrl}/mfa/verify`);
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual({ code: "123456" });
    request.flush(null);
  });
});
