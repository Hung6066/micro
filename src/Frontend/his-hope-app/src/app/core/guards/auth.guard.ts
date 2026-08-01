import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { Observable, map, switchMap } from 'rxjs';
import { AuthService } from '@core/services/auth.service';

/**
 * Authentication guard that checks if the user is authenticated.
 * Redirects to `/auth/login` if not authenticated.
 */
export const authGuard: CanActivateFn = (): Observable<boolean | UrlTree> => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.checkAuth().pipe(
    switchMap(() => authService.isLoggedIn()),
    map(isAuth => (isAuth ? true : router.parseUrl('/auth/login'))),
  );
};
