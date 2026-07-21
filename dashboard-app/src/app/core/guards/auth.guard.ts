import { inject } from '@angular/core';
import { Router, type CanActivateFn } from '@angular/router';
import { filter, map, take } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.isAuthenticated$.pipe(
    filter((isAuth) => isAuth !== null),
    take(1),
    map(isAuthenticated => {
      if (!isAuthenticated) {
        authService.login(router.url);
        return false;
      }
      return true;
    })
  );
};
