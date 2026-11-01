import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { TenantContextService } from "../services/tenant-context.service";
import { shouldAttachTenantScopeId } from "../utils/tenant-scope.util";

/** Injects active tenant scopeId into admin API requests unless already present. */
export const tenantScopeInterceptor: HttpInterceptorFn = (request, next) => {
  if (!shouldAttachTenantScopeId(request.url)) {
    return next(request);
  }

  if (request.params.has("scopeId")) {
    return next(request);
  }

  const scopeId = inject(TenantContextService).getActiveTenantScopeId();
  if (!scopeId) {
    return next(request);
  }

  return next(
    request.clone({
      params: request.params.set("scopeId", scopeId),
    }),
  );
};
