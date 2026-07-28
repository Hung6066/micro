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
});
