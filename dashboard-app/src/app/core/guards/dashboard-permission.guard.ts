import { inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, UrlTree } from '@angular/router';
import { Observable, catchError, map, of } from 'rxjs';
import { HisHopePermissionService } from '@his-hope/frontend-foundation/auth';
import { environment } from '../../../environments/environment';

interface PermissionSnapshotResponse {
  userId?: string;
  userName?: string;
  roles?: string[];
  permissions: string[];
  facilityIds?: string[];
  authzVersion?: string;
  issuedAt?: string;
  expiresAt?: string;
}

/** Hydrates the shared permission snapshot before any dashboard route loads. */
export const dashboardPermissionGuard = (): Observable<boolean | UrlTree> => {
  const http = inject(HttpClient);
  const permissions = inject(HisHopePermissionService);
  const router = inject(Router);

  if (permissions.has('dashboard.view')) return of(true);

  return http
    .get<PermissionSnapshotResponse>(
      `${environment.apiUrl}/v1/admin/me/permissions`,
    )
    .pipe(
      map((snapshot) => {
        permissions.setSnapshot(snapshot);
        return permissions.has('dashboard.view')
          ? true
          : router.parseUrl('/access-denied');
      }),
      catchError(() => of(router.parseUrl('/access-denied'))),
    );
};
