import { HttpInterceptorFn } from "@angular/common/http";
import { createHisHopeOperatorTenantInterceptor } from "@his-hope/frontend-foundation";
import { BUYER_TENANT_KEY } from "../services/content-api.service";

/**
 * Buyer is a single-tenant portal. Keep the tenant identity in the canonical
 * header for every manufacturing/commerce/content request and never in URLs.
 */
export const tenantContextInterceptor: HttpInterceptorFn =
  createHisHopeOperatorTenantInterceptor({
    urlIncludes: "/api/v1/",
    getActiveTenantKey: () => BUYER_TENANT_KEY,
  });
