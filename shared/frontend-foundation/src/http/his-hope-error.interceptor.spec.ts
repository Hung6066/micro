import { TestBed, fakeAsync, tick } from "@angular/core/testing";
import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { hisHopeErrorInterceptor } from "./his-hope-error.interceptor";
import { HisHopeErrorReportingService } from "./his-hope-error-reporting.service";

describe("hisHopeErrorInterceptor", () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;
  let errorReporting: HisHopeErrorReportingService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([hisHopeErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);
    errorReporting = TestBed.inject(HisHopeErrorReportingService);
  });

  afterEach(() => httpMock.verify());

  it("retries a transient 503 on a GET and resolves without the caller seeing an error", fakeAsync(() => {
    let result: unknown;
    http.get("/api/v1/clients").subscribe((value) => (result = value));

    httpMock
      .expectOne("/api/v1/clients")
      .flush("fail", { status: 503, statusText: "Service Unavailable" });
    tick(5000);
    httpMock.expectOne("/api/v1/clients").flush({ ok: true });

    expect(result).toEqual({ ok: true });
    expect(errorReporting.events().length).toBe(0);
  }));

  it("does not retry a non-idempotent POST even on a transient status", () => {
    let error: HttpErrorResponse | undefined;
    http
      .post("/api/v1/clients", {})
      .subscribe({ error: (err) => (error = err) });

    httpMock
      .expectOne("/api/v1/clients")
      .flush("fail", { status: 503, statusText: "Service Unavailable" });

    expect(error?.status).toBe(503);
    expect(errorReporting.events().length).toBe(1);
  });

  it("reports and rethrows a terminal 404 without retrying", () => {
    let error: HttpErrorResponse | undefined;
    http
      .get("/api/v1/clients/missing")
      .subscribe({ error: (err) => (error = err) });

    httpMock
      .expectOne("/api/v1/clients/missing")
      .flush("not found", { status: 404, statusText: "Not Found" });

    expect(error?.status).toBe(404);
    const [event] = errorReporting.events();
    expect(event.statusCode).toBe(404);
    expect(event.severity).toBe("warning");
  });

  it("gives up after the retry budget and reports exactly one final failure", fakeAsync(() => {
    let error: HttpErrorResponse | undefined;
    http.get("/api/v1/clients").subscribe({ error: (err) => (error = err) });

    httpMock
      .expectOne("/api/v1/clients")
      .flush("fail", { status: 503, statusText: "Service Unavailable" });
    tick(5000);
    httpMock
      .expectOne("/api/v1/clients")
      .flush("fail", { status: 503, statusText: "Service Unavailable" });
    tick(5000);
    httpMock
      .expectOne("/api/v1/clients")
      .flush("fail", { status: 503, statusText: "Service Unavailable" });

    expect(error?.status).toBe(503);
    expect(errorReporting.events().length).toBe(1);
  }));
});
