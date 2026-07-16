import { Injectable } from '@angular/core';
import {
  CanActivate,
  CanActivateChild,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
  UrlTree,
} from '@angular/router';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';
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
  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  canActivate(
    route: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkRoles(route);
  }

  canActivateChild(
    childRoute: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkRoles(childRoute);
  }

  private checkRoles(route: ActivatedRouteSnapshot): Observable<boolean | UrlTree> {
    const requiredRoles: string[] = route.data?.['roles'];

    // No roles specified — allow through
    if (!requiredRoles || requiredRoles.length === 0) {
      return this.authService.isLoggedIn().pipe(
        map((loggedIn) => {
          if (!loggedIn) {
            return this.router.parseUrl('/auth/login');
          }
          return true;
        }),
      );
    }

    return this.authService.currentUser$.pipe(
      take(1),
      map((user) => {
        if (!user) {
          return this.router.parseUrl('/auth/login');
        }

        const userRoles = user.roles ?? [];
        const hasRole = requiredRoles.some((role) =>
          userRoles.some(
            (ur) => ur.toLowerCase() === role.toLowerCase(),
          ),
        );

        if (!hasRole) {
          return this.router.parseUrl('/access-denied');
        }

        return true;
      }),
    );
  }
}
