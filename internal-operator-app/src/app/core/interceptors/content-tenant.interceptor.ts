import { inject } from "@angular/core";
import { createHisHopeOperatorTenantInterceptor } from "@his-hope/frontend-foundation";
import { TenantContextService } from "../services/tenant-context.service";
import { environment } from "../../../environments/environment";

/** Attach canonical tenant context for content APIs when viewing a customer tenant. */
export const contentTenantInterceptor = createHisHopeOperatorTenantInterceptor(() => ({
  urlIncludes: "/api/v1/content",
  getActiveTenantKey: () => inject(TenantContextService).getActiveTenantKey(),
  homeTenantKey: environment.homeTenantKey,
}));
