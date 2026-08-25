import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { TenantContextService } from "../services/tenant-context.service";
import { environment } from "../../../environments/environment";

/** Attach tenantKey for commerce APIs when operator views a customer tenant. */
export const commerceTenantInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.includes("/api/v1/commerce")) {
    return next(request);
  }

  if (request.params.has("tenantKey")) {
    return next(request);
  }

  const activeKey = inject(TenantContextService).getActiveTenantKey();
  if (
    activeKey &&
    activeKey !== environment.homeTenantKey
  ) {
    return next(
      request.clone({
        params: request.params.set("tenantKey", activeKey),
      }),
    );
  }

  return next(request);
};
