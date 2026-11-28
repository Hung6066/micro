import { inject } from "@angular/core";
import { createHisHopeOperatorTenantInterceptor } from "@his-hope/frontend-foundation";
import { OperatorMobileTenantContextService } from "../operator-mobile-tenant-context.service";
import { environment } from "../../../environments/environment";

/** Attach canonical tenant context for manufacturing APIs when viewing a customer tenant. */
export const operatorMobileManufacturingTenantInterceptor =
  createHisHopeOperatorTenantInterceptor(() => ({
    urlIncludes: "/api/v1/manufacturing",
    getActiveTenantKey: () =>
      inject(OperatorMobileTenantContextService).getActiveTenantKey(),
    homeTenantKey: environment.homeTenantKey,
  }));
