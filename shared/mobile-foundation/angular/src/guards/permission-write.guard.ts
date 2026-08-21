import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";

export interface HisHopePermissionWriteGuardOptions {
  readonly fallbackPath: string | ((route: { data: Record<string, unknown> }) => string);
}

/** Factory for permission-aware mobile mutation routes. */
export function createHisHopePermissionWriteGuard(
  options: HisHopePermissionWriteGuardOptions,
): CanActivateFn {
  return (route) => {
    const permissions = inject(HisHopePermissionService);
    const router = inject(Router);
    const permission = route.data["writePermission"] as string | undefined;
    if (!permission || permissions.has(permission)) return true;
    const fallback =
      typeof options.fallbackPath === "function"
        ? options.fallbackPath(route)
        : ((route.data["resourceListPath"] as string | undefined) ??
          options.fallbackPath);
    return router.createUrlTree([fallback]);
  };
}
