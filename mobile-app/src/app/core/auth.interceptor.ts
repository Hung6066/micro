import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { switchMap } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.includes('/api/')) return next(request);
  return inject(OidcSecurityService).getAccessToken().pipe(
    switchMap(token => token ? next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })) : next(request)),
  );
};
