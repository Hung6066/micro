import { inject } from '@angular/core';
import { Router, UrlTree } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { HisHopePermissionService } from '@his-hope/frontend-foundation';
import { AdminApiService } from '../services/admin-api.service';

/**
 * Client-side navigation hint only. Identity Service remains the authority and
 * still enforces the permission on every endpoint.
 */
export const capabilityPermissionGuard = (): Observable<boolean | UrlTree> => {
  const permissions = inject(HisHopePermissionService);
  const api = inject(AdminApiService);
  const router = inject(Router);
  const required = 'admin.settings.read';

  if (permissions.has(required)) return of(true);

  return api.getMyPermissions().pipe(
    tap(snapshot => permissions.setSnapshot(snapshot)),
    map(snapshot => permissions.has(required)
      ? true
      : router.createUrlTree(['/forbidden'], { queryParams: { capability: 'identity' } })),
    catchError(() => of(router.createUrlTree(['/forbidden'], { queryParams: { capability: 'identity' } }))),
  );
};
