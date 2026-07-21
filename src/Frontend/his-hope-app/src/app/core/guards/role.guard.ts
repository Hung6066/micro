import { inject, Injectable } from '@angular/core';
import {
  CanActivate,
  CanActivateChild,
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
 * Implements both CanActivate and CanActivateChild to protect parent and
 * child routes uniformly.
 */
@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate, CanActivateChild {
  private authService = inject(AuthService);
  private router = inject(Router);

  canActivate(
    route: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkRoles(route, _state);
  }

  canActivateChild(
    childRoute: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkRoles(childRoute, _state);
  }

  private checkRoles(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    const requiredRoles: string[] = route.data?.['roles'];

    return this.authService.ensureCurrentUser().pipe(
      map((user) => {
        if (!user) {
          return this.router.createUrlTree(['/auth/login'], {
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
          return this.router.createUrlTree(['/access-denied']);
        }

        return true;
      }),
    );
  }
}
