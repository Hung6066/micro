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
 * Permission-based guard that checks if the authenticated user has ALL
 * required permissions specified in route data:
 * `data: { permissions: ['patients.view'] }`.
 *
 * Uses AND logic — the user must have every listed permission.
 * Implements both CanActivate and CanActivateChild.
 */
@Injectable({ providedIn: 'root' })
export class PermissionGuard implements CanActivate, CanActivateChild {
  constructor(
    private authService: AuthService,
    private router: Router,
  ) {}

  canActivate(
    route: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkPermissions(route);
  }

  canActivateChild(
    childRoute: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkPermissions(childRoute);
  }

  private checkPermissions(
    route: ActivatedRouteSnapshot,
  ): Observable<boolean | UrlTree> {
    const requiredPermissions: string[] = route.data?.['permissions'];

    // No permissions specified — allow through
    if (!requiredPermissions || requiredPermissions.length === 0) {
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

        const userPermissions = this.authService.getUserPermissions();
        const hasAllPermissions = requiredPermissions.every((perm) =>
          userPermissions.includes(perm),
        );

        if (!hasAllPermissions) {
          return this.router.parseUrl('/access-denied');
        }

        return true;
      }),
    );
  }
}
