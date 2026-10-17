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
import { OperatorMobileTenantContextService } from "./operator-mobile-tenant-context.service";
import { MobileAuthService } from "./auth.service";
import { OperationQueueService } from "./offline/operation-queue.service";
import { operatorMobileManufacturingTenantInterceptor } from "./interceptors/operator-mobile-manufacturing-tenant.interceptor";
import { environment } from "../../environments/environment";
import { Subject } from "rxjs";

describe("OperatorMobileTenantContextService", () => {
  let service: OperatorMobileTenantContextService;
  let httpMock: HttpTestingController;
  const userData$ = new Subject<{ userData?: Record<string, unknown> } | null>();

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        OperatorMobileTenantContextService,
        {
          provide: MobileAuthService,
          useValue: {
            userData$,
            getCurrentUserProfile: () => new Subject(),
          },
        },
        {
          provide: OperationQueueService,
          useValue: { retainScope: jasmine.createSpy("retainScope").and.resolveTo(undefined) },
        },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(OperatorMobileTenantContextService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it("loads switchable tenants and persists the selected tenant", async () => {
    userData$.next({ userData: { sub: "operator-1", email: "op@example.com" } });
    const initPromise = service.initialize();
    const req = httpMock.expectOne(`${environment.adminApiUrl}/me/switchable-tenants`);
    req.flush({
      tenants: [
        {
          key: "manufacturing",
          displayName: "Manufacturing HQ",
          scopeId: "scope-1",
          tenantClass: "operator",
          isCustomerSupport: false,
        },
        {
          key: "customer-factory-x",
          displayName: "Factory X",
          scopeId: "scope-2",
          tenantClass: "customer",
          isCustomerSupport: true,
        },
      ],
    });
    await initPromise;

    expect(service.tenantOptions()).toEqual([
      { key: "manufacturing", label: "Manufacturing HQ" },
      { key: "customer-factory-x", label: "Factory X" },
    ]);
    expect(service.getActiveTenantKey()).toBe("manufacturing");
    expect(sessionStorage.getItem("operatorMobile.activeTenantKey")).toBe("manufacturing");
  });

  it("falls back to JWT tenant claims when switchable tenants are unavailable", async () => {
    userData$.next({
      userData: {
        sub: "operator-1",
        tenant_id: "manufacturing",
        tenant_membership: "customer-factory-x",
      },
    });
    const initPromise = service.initialize();
    const req = httpMock.expectOne(`${environment.adminApiUrl}/me/switchable-tenants`);
    req.error(new ProgressEvent("error"));
    await initPromise;

    expect(service.tenantOptions().map((option) => option.key)).toEqual([
      "manufacturing",
      "customer-factory-x",
    ]);
  });
});

describe("operatorMobileManufacturingTenantInterceptor", () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;
  const activeTenantKey: string | null = "customer-factory-x";

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: OperatorMobileTenantContextService,
          useValue: {
            getActiveTenantKey: () => activeTenantKey,
          },
        },
        provideHttpClient(
          withInterceptors([operatorMobileManufacturingTenantInterceptor]),
        ),
        provideHttpClientTesting(),
      ],
    });
    httpMock = TestBed.inject(HttpTestingController);
    http = TestBed.inject(HttpClient);
  });

  afterEach(() => httpMock.verify());

  it("adds the canonical tenant header to manufacturing traceability reads", () => {
    http.get("/api/v1/manufacturing/lots/abc/genealogy").subscribe();
    const req = httpMock.expectOne((request) =>
      request.headers.get("X-HisHope-Tenant") === "customer-factory-x",
    );
    expect(req.request.url).toContain("/lots/abc/genealogy");
    req.flush({ lot: {}, relations: [] });
  });
});
