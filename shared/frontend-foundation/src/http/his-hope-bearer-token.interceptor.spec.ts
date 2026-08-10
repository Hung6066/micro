import { TestBed } from "@angular/core/testing";
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { of } from "rxjs";
import { createHisHopeBearerTokenInterceptor } from "./his-hope-bearer-token.interceptor";

describe("createHisHopeBearerTokenInterceptor", () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;

  afterEach(() => httpMock.verify());

  it("attaches a bearer token and credentials to /api/ requests by default", () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeBearerTokenInterceptor(() => of("token-123")),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    http.get("/api/v1/clients").subscribe();
    const req = httpMock.expectOne("/api/v1/clients");
    expect(req.request.headers.get("Authorization")).toBe("Bearer token-123");
    expect(req.request.withCredentials).toBeTrue();
    req.flush({});
  });

  it("leaves non-matching requests untouched apart from withCredentials", () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeBearerTokenInterceptor(() => of("token-123")),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    http.get("/assets/logo.png").subscribe();
    const req = httpMock.expectOne("/assets/logo.png");
    expect(req.request.headers.has("Authorization")).toBeFalse();
    req.flush({});
  });

  it("omits the Authorization header when no token is available", () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([createHisHopeBearerTokenInterceptor(() => of(""))]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    http.get("/api/v1/clients").subscribe();
    const req = httpMock.expectOne("/api/v1/clients");
    expect(req.request.headers.has("Authorization")).toBeFalse();
    req.flush({});
  });

  it("honors a custom matcher and a withCredentials override", () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeBearerTokenInterceptor(() => of("t"), {
              matches: (url) => url.includes("/secure/"),
              withCredentials: false,
            }),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    http.get("/secure/data").subscribe();
    const req = httpMock.expectOne("/secure/data");
    expect(req.request.headers.get("Authorization")).toBe("Bearer t");
    expect(req.request.withCredentials).toBeFalse();
    req.flush({});
  });

  it("supports the DPoP authorization scheme for sender-constrained tokens", () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeBearerTokenInterceptor(() => of("proof-bound-token"), {
              tokenType: "DPoP",
              withCredentials: false,
            }),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    http.get("/api/v1/clients").subscribe();
    const req = httpMock.expectOne("/api/v1/clients");
    expect(req.request.headers.get("Authorization")).toBe("DPoP proof-bound-token");
    req.flush({});
  });

  it("refreshes once and retries a request after a 401", () => {
    let refreshCalls = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeBearerTokenInterceptor(() => of("fresh-token"), {
              refreshAccessToken: () => {
                refreshCalls += 1;
                return of(true);
              },
            }),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    let response: unknown;
    http.get("/api/v1/clients").subscribe((value) => { response = value; });

    const first = httpMock.expectOne("/api/v1/clients");
    first.flush({ message: "expired" }, { status: 401, statusText: "Unauthorized" });
    const retryRequest = httpMock.expectOne("/api/v1/clients");
    expect(retryRequest.request.headers.get("Authorization")).toBe("Bearer fresh-token");
    retryRequest.flush({ ok: true });

    expect(refreshCalls).toBe(1);
    expect(response).toEqual({ ok: true });
  });

  it("notifies session expiry when refresh cannot recover a 401", () => {
    let sessionExpired = 0;
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeBearerTokenInterceptor(() => of("expired-token"), {
              refreshAccessToken: () => of(false),
              onSessionExpired: () => { sessionExpired += 1; },
            }),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);

    let status = 0;
    http.get("/api/v1/clients").subscribe({ error: (error) => { status = error.status; } });
    httpMock.expectOne("/api/v1/clients").flush({}, { status: 401, statusText: "Unauthorized" });

    expect(status).toBe(401);
    expect(sessionExpired).toBe(1);
  });
});
