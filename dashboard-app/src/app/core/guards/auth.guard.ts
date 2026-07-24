import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { filter, switchMap, tap } from 'rxjs/operators';

export const authGuard = () => {
  const authService = inject(AuthService);
  const oidcSecurityService = inject(OidcSecurityService);

  return authService.checkAuth().pipe(
    switchMap(() => authService.isAuthenticated$),
    tap(isAuth => { if (!isAuth) oidcSecurityService.authorize(); }),
    filter(isAuth => isAuth),
  );
};
