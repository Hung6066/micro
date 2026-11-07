import { inject } from "@angular/core";
import { createHisHopeOperatorTenantInterceptor } from "@his-hope/frontend-foundation";
import { TenantContextService } from "../services/tenant-context.service";
import { environment } from "../../../environments/environment";

/** Attach canonical tenant context for commerce APIs when operator views a customer tenant. */
export const commerceTenantInterceptor = createHisHopeOperatorTenantInterceptor(() => ({
  urlIncludes: "/api/v1/commerce",
  getActiveTenantKey: () => inject(TenantContextService).getActiveTenantKey(),
  homeTenantKey: environment.homeTenantKey,
}));
