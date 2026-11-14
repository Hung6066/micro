import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { catchError, of, switchMap, tap } from "rxjs";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { readPermission } from "./authorization/mobile-read-permissions";
import type { OperatorMobileArea } from "./contracts/mobile.contracts";
import { MobileAdminApiService } from "./admin-api.service";

export const mobileReadGuard = (route: Parameters<CanActivateFn>[0]) => {
  const permissions = inject(HisHopePermissionService);
  const api = inject(MobileAdminApiService);
  const router = inject(Router);
  const area = route.data["area"] as OperatorMobileArea | undefined;
  const permission =
    (route.data["readPermission"] as string | undefined) ??
    (area ? readPermission(area) : undefined);

  if (!permission) return true;

  const forbidden = () =>
    router.createUrlTree(["/operations/forbidden"], {
      queryParams: {
        resource:
          (route.data["area"] as string | undefined) ??
          (route.data["resource"] as string | undefined) ??
          permission,
      },
    });

  if (permissions.hasSnapshot())
    return permissions.has(permission) ? true : forbidden();

  return api.getMyPermissions().pipe(
    tap((snapshot) => {
      if (snapshot) permissions.setPermissions(snapshot.permissions);
    }),
    switchMap(() => of(permissions.has(permission) ? true : forbidden())),
    catchError(() => of(forbidden())),
  );
};
