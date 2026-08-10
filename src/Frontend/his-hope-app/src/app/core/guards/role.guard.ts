import { inject } from '@angular/core';
import {
  CanActivateFn,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
  UrlTree,
} from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

/**
 * Role-based guard that checks if the authenticated user has one of the
 * required roles specified in route data: `data: { roles: ['Admin'] }`.
 *
 * Redirects to `/auth/login` if no user, or `/access-denied` if the user
 * lacks the required role.
 */
export const roleGuard: CanActivateFn = (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot,
): Observable<boolean | UrlTree> => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const requiredRoles: string[] = route.data?.['roles'];

  return authService.ensureCurrentUser().pipe(
    map((user) => {
      if (!user) {
        return router.createUrlTree(['/auth/login'], {
          queryParams: { returnUrl: state.url },
        });
      }

      // No roles specified — allow through once auth is hydrated.
      if (!requiredRoles || requiredRoles.length === 0) {
        return true;
      }

      const userRoles = user.roles ?? [];
      const hasRole = requiredRoles.some((role) =>
        userRoles.some(
          (ur) => ur.toLowerCase() === role.toLowerCase(),
        ),
      );

      if (!hasRole) {
        return router.createUrlTree(['/access-denied']);
      }

      return true;
    }),
  );
};
