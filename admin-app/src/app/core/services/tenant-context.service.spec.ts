import { TestBed } from "@angular/core/testing";
import { firstValueFrom, of } from "rxjs";
import { AdminPermissionsApiService } from "./admin-permissions-api.service";
import { IamApiService } from "./iam-api.service";
import { TenantContextService } from "./tenant-context.service";

describe("TenantContextService HQ tenant", () => {
  beforeEach(() => {
    sessionStorage.removeItem("admin.activeTenantKey");
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AdminPermissionsApiService,
          useValue: {
            getCurrent: () => of({ tenantMemberships: ["manufacturing", "nacoms-group"] }),
          },
        },
        { provide: IamApiService, useValue: { getIamScopes: () => of([]) } },
      ],
    });
  });

  afterEach(() => sessionStorage.removeItem("admin.activeTenantKey"));

  it("recognizes Nacoms Group and prefers it as the active tenant", async () => {
    const service = TestBed.inject(TenantContextService);
    await firstValueFrom(service.initialize());
    expect(service.isGroupHqOperator()).toBeTrue();
    expect(service.getActiveTenantKey()).toBe("nacoms-group");
  });

  it("does not grant HQ access to the retired key", async () => {
    TestBed.overrideProvider(AdminPermissionsApiService, {
      useValue: { getCurrent: () => of({ tenantMemberships: ["group-hq"] }) },
    });
    const service = TestBed.inject(TenantContextService);
    await firstValueFrom(service.initialize());
    expect(service.isGroupHqOperator()).toBeFalse();
  });
});
