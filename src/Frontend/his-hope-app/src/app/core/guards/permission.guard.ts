import { inject, Injectable } from '@angular/core';
import {
  CanActivate,
  CanActivateChild,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
  UrlTree,
} from '@angular/router';
import { Observable, of } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

/**
 * Permission-based guard that checks if the authenticated user has ALL
 * required permissions specified in route data:
 * `data: { permissions: ['patients.view'] }`.
 *
 * Uses AND logic — the user must have every listed permission.
 * Checks permissions locally from the JWT-sourced user object
 * (the backend enforces permissions on actual API calls).
 * Implements both CanActivate and CanActivateChild.
 */
@Injectable({ providedIn: 'root' })
export class PermissionGuard implements CanActivate, CanActivateChild {
  private authService = inject(AuthService);
  private router = inject(Router);

  canActivate(
    route: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkPermissions(route, _state);
  }

  canActivateChild(
    childRoute: ActivatedRouteSnapshot,
    _state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    return this.checkPermissions(childRoute, _state);
  }

  private checkPermissions(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot,
  ): Observable<boolean | UrlTree> {
    const requiredPermissions: string[] = route.data?.['permissions'];

    return this.authService.ensureCurrentUser().pipe(
      switchMap((user) => {
        if (!user) {
          return of(
            this.router.createUrlTree(['/auth/login'], {
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
        const allowed = this.authService.hasPermission(requiredPermissions);
        if (!allowed) {
          return of(this.router.createUrlTree(['/access-denied']));
        }
        return of(true);
      }),
    );
  }
}
