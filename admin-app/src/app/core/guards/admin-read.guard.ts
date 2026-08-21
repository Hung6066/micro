import { inject } from "@angular/core";
import { ActivatedRouteSnapshot, CanActivateFn, Router } from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { Observable, of } from "rxjs";
import { catchError, map, switchMap, tap } from "rxjs/operators";
import { resolveAdminRoutePermission } from "../authorization/admin-route-permissions";
import { AdminPermissionsApiService } from "../services/admin-permissions-api.service";

function requiredPermission(route: ActivatedRouteSnapshot): string | undefined {
  return (
    (route.data["readPermission"] as string | undefined) ??
    resolveAdminRoutePermission(route.routeConfig?.path ?? "")
  );
}

function isAllowed(
  permissions: HisHopePermissionService,
  permission: string | undefined,
): boolean {
  return !permission || permissions.has(permission);
}

/**
 * Client-side navigation guard. Identity Service remains authoritative on API
 * calls; this guard prevents loading IAM surfaces without the read permission.
 */
export const adminReadGuard: CanActivateFn = (route) => {
  const permissions = inject(HisHopePermissionService);
  const api = inject(AdminPermissionsApiService);
  const router = inject(Router);
  const permission = requiredPermission(route);

  if (isAllowed(permissions, permission)) return of(true);

  const hydrateAndCheck = (): Observable<boolean | import("@angular/router").UrlTree> =>
    api.getCurrent().pipe(
      tap((snapshot) => permissions.setSnapshot(snapshot)),
      map(() =>
        isAllowed(permissions, permission)
          ? true
          : router.createUrlTree(["/forbidden"], {
              queryParams: { permission: permission ?? "admin" },
            }),
      ),
      catchError(() =>
        of(
          router.createUrlTree(["/forbidden"], {
            queryParams: { permission: permission ?? "admin" },
          }),
        ),
      ),
    );

  if (permissions.hasSnapshot()) {
    return of(
      router.createUrlTree(["/forbidden"], {
        queryParams: { permission: permission ?? "admin" },
      }),
    );
  }

  return hydrateAndCheck();
};
