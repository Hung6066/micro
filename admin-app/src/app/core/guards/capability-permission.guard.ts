import { inject } from "@angular/core";
import { Router, UrlTree } from "@angular/router";
import { Observable, of } from "rxjs";
import { catchError, map, tap } from "rxjs/operators";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { AdminPermissionsApiService } from "../services/admin-permissions-api.service";

/**
 * Client-side navigation hint only. Identity Service remains the authority and
 * still enforces the permission on every endpoint.
 */
export const capabilityPermissionGuard = (): Observable<boolean | UrlTree> => {
  const permissions = inject(HisHopePermissionService);
  const api = inject(AdminPermissionsApiService);
  const router = inject(Router);
  const required = "admin.settings.read";

  if (permissions.has(required)) return of(true);

  return api.getCurrent().pipe(
    tap((snapshot) => permissions.setSnapshot(snapshot)),
    map(() =>
      permissions.has(required)
        ? true
        : router.createUrlTree(["/forbidden"], {
            queryParams: { capability: "identity" },
          }),
    ),
    catchError(() =>
      of(
        router.createUrlTree(["/forbidden"], {
          queryParams: { capability: "identity" },
        }),
      ),
    ),
  );
};
