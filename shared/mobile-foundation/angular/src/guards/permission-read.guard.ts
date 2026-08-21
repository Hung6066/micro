import { inject } from "@angular/core";
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
} from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";

export interface HisHopePermissionReadGuardOptions {
  readonly forbiddenPath: string;
  readonly resolvePermission: (
    route: ActivatedRouteSnapshot,
  ) => string | undefined;
  readonly resolveForbiddenQuery?: (
    route: ActivatedRouteSnapshot,
    permission: string,
  ) => Record<string, string>;
}

/** Factory for permission-aware mobile read routes. */
export function createHisHopePermissionReadGuard(
  options: HisHopePermissionReadGuardOptions,
): CanActivateFn {
  return (route) => {
    const permissions = inject(HisHopePermissionService);
    const router = inject(Router);
    const permission = options.resolvePermission(route);
    if (!permission || permissions.has(permission)) return true;
    const queryParams =
      options.resolveForbiddenQuery?.(route, permission) ?? {
        resource: (route.data["resource"] as string | undefined) ?? permission,
      };
    return router.createUrlTree([options.forbiddenPath], { queryParams });
  };
}
