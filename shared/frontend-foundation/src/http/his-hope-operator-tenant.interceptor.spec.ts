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
import { createHisHopeOperatorTenantInterceptor } from "./his-hope-operator-tenant.interceptor";

describe("createHisHopeOperatorTenantInterceptor", () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;
  let activeTenantKey: string | null = "customer-factory-x";

  beforeEach(() => {
    activeTenantKey = "customer-factory-x";
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(
          withInterceptors([
            createHisHopeOperatorTenantInterceptor(() => ({
              urlIncludes: "/api/v1/manufacturing",
              getActiveTenantKey: () => activeTenantKey,
              homeTenantKey: "manufacturing",
            })),
          ]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);
  });

  afterEach(() => httpMock.verify());

  it("attaches the canonical tenant header for cross-tenant requests", () => {
    http.get("/api/v1/manufacturing/lots").subscribe();
    const req = httpMock.expectOne("/api/v1/manufacturing/lots");
    expect(req.request.method).toBe("GET");
    expect(req.request.headers.get("X-HisHope-Tenant")).toBe("customer-factory-x");
    req.flush([]);
  });

  it("skips tenant context when active tenant is home", () => {
    activeTenantKey = "manufacturing";
    http.get("/api/v1/manufacturing/lots").subscribe();
    const req = httpMock.expectOne("/api/v1/manufacturing/lots");
    expect(req.request.urlWithParams).not.toContain("tenantKey=");
    expect(req.request.headers.has("X-HisHope-Tenant")).toBeFalse();
    req.flush([]);
  });

  it("ignores unrelated URLs", () => {
    http.get("/api/v1/admin/me/permissions").subscribe();
    const req = httpMock.expectOne("/api/v1/admin/me/permissions");
    expect(req.request.urlWithParams).not.toContain("tenantKey=");
    expect(req.request.headers.has("X-HisHope-Tenant")).toBeFalse();
    req.flush({});
  });

  it("removes an explicit legacy tenantKey when adding canonical context", () => {
    http
      .get("/api/v1/manufacturing/lots", {
        params: { tenantKey: "explicit-tenant" },
      })
      .subscribe();
    const req = httpMock.expectOne("/api/v1/manufacturing/lots");
    expect(req.request.headers.get("X-HisHope-Tenant")).toBe("customer-factory-x");
    expect(req.request.urlWithParams).not.toContain("tenantKey=");
    req.flush([]);
  });
});
