import { provideHttpClient } from "@angular/common/http";
import { provideHttpClientTesting, HttpTestingController } from "@angular/common/http/testing";
import { TestBed } from "@angular/core/testing";
import { environment } from "../../../environments/environment";
import { PasskeyApiService } from "./passkey-api.service";

describe("PasskeyApiService", () => {
  let service: PasskeyApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PasskeyApiService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PasskeyApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("loads passkey status", () => {
    service.getStatus().subscribe((status) => expect(status.registered).toBeTrue());

    const request = http.expectOne(`${environment.authApiUrl}/passkeys/status`);
    expect(request.request.method).toBe("GET");
    request.flush({ registered: true });
  });

  it("decodes registration options at the HTTP boundary", () => {
    let options: PublicKeyCredentialCreationOptions | undefined;
    service.getRegistrationOptions().subscribe((result) => (options = result));

    const request = http.expectOne(`${environment.authApiUrl}/passkeys/register/options`);
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual({});
    request.flush({
      challenge: "AQI",
      rp: { name: "His.Hope", id: "localhost" },
      user: { id: "AwQ", name: "admin", displayName: "Admin" },
      pubKeyCredParams: [{ type: "public-key", alg: -7 }],
      excludeCredentials: [{ id: "BQY", type: "public-key" }],
    });

    expect(Array.from(options!.challenge as Uint8Array)).toEqual([1, 2]);
    expect(Array.from(options!.user.id as Uint8Array)).toEqual([3, 4]);
    expect(Array.from(options!.excludeCredentials![0].id as Uint8Array)).toEqual([5, 6]);
  });

  it("completes registration with the typed payload", () => {
    const payload = {
      id: "credential-id",
      rawId: "AQI",
      type: "public-key",
      response: { clientDataJSON: "AwQ", attestationObject: "BQY" },
    };
    service.completeRegistration(payload).subscribe();

    const request = http.expectOne(`${environment.authApiUrl}/passkeys/register/complete`);
    expect(request.request.method).toBe("POST");
    expect(request.request.body).toEqual(payload);
    request.flush(null);
  });
});
