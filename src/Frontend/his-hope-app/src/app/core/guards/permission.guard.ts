import { inject } from '@angular/core';
import {
  CanActivateFn,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
  UrlTree,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

/**
 * Permission-based guard that checks if the authenticated user has ALL
 * required permissions specified in route data:
 * `data: { permissions: ['patients.view'] }`.
 *
 * Uses AND logic — the user must have every listed permission.
 * Checks permissions locally from the JWT-sourced user object
 * (the backend enforces permissions on actual API calls).
 *
 * Redirects to `/auth/login` if no user, or `/access-denied` if the user
 * lacks the required permissions.
 */
export const permissionGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot,
): Observable<boolean | UrlTree> => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const requiredPermissions: string[] = route.data?.['permissions'];

  return authService.ensureCurrentUser().pipe(
    switchMap((user) => {
      if (!user) {
        return of(
          router.createUrlTree(['/auth/login'], {
            queryParams: { returnUrl: state.url },
          }),
        );
      }

      // No permissions specified — allow through once auth is hydrated.
      if (!requiredPermissions || requiredPermissions.length === 0) {
        return of(true);
      }

      // Check permissions locally from JWT-sourced user object.
      // The backend enforces permissions on actual API calls.
      const allowed = authService.hasPermission(requiredPermissions);
      if (!allowed) {
        return of(router.createUrlTree(['/access-denied']));
      }
      return of(true);
    }),
  );
};
