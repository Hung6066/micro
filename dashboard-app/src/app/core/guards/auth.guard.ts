import { inject } from '@angular/core';
import { Router, UrlTree } from '@angular/router';
import { Observable, map, switchMap } from 'rxjs';
import { AuthService } from '../services/auth.service';

// Must resolve to true|UrlTree on every path — a guard observable that never
// emits for the unauthenticated case leaves the router navigation hanging.
export const authGuard = (): Observable<boolean | UrlTree> => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.checkAuth().pipe(
    switchMap(() => authService.isAuthenticated$),
    map((isAuth) => (isAuth ? true : router.parseUrl('/auth/login'))),
  );
};
