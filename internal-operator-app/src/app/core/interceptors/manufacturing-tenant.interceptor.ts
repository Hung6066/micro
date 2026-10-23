import { inject } from "@angular/core";
import { createHisHopeOperatorTenantInterceptor } from "@his-hope/frontend-foundation";
import { TenantContextService } from "../services/tenant-context.service";
import { environment } from "../../../environments/environment";

/** Attach canonical tenant context for manufacturing APIs when viewing a customer tenant. */
export const manufacturingTenantInterceptor = createHisHopeOperatorTenantInterceptor(() => ({
  urlIncludes: "/api/v1/manufacturing",
  getActiveTenantKey: () => inject(TenantContextService).getActiveTenantKey(),
  homeTenantKey: environment.homeTenantKey,
}));
