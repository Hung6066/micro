import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { TenantContextService } from "../services/tenant-context.service";

/** Restricts platform-level surfaces to Nacoms Group operators. */
export const hqOperatorGuard: CanActivateFn = () => {
  const tenantContext = inject(TenantContextService);
  const router = inject(Router);
  return tenantContext.isGroupHqOperator()
    ? true
    : router.createUrlTree(["/forbidden"], {
        queryParams: { permission: "nacoms-group" },
      });
};
