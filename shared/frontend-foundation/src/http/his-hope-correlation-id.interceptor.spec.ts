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
import { hisHopeCorrelationIdInterceptor } from "./his-hope-correlation-id.interceptor";

describe("hisHopeCorrelationIdInterceptor", () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([hisHopeCorrelationIdInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);
  });

  afterEach(() => httpMock.verify());

  it("stamps a correlation id header on every request", () => {
    http.get("/api/v1/clients").subscribe();
    const req = httpMock.expectOne("/api/v1/clients");
    expect(req.request.headers.get("X-Correlation-Id")).toBeTruthy();
    req.flush({});
  });

  it("does not overwrite an id the caller already set", () => {
    http
      .get("/api/v1/clients", { headers: { "X-Correlation-Id": "preset" } })
      .subscribe();
    const req = httpMock.expectOne("/api/v1/clients");
    expect(req.request.headers.get("X-Correlation-Id")).toBe("preset");
    req.flush({});
  });

  it("generates distinct ids for distinct requests", () => {
    http.get("/a").subscribe();
    http.get("/b").subscribe();
    const reqA = httpMock.expectOne("/a");
    const reqB = httpMock.expectOne("/b");
    expect(reqA.request.headers.get("X-Correlation-Id")).not.toBe(
      reqB.request.headers.get("X-Correlation-Id"),
    );
    reqA.flush({});
    reqB.flush({});
  });
});
