import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { switchMap } from 'rxjs/operators';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/')) return next(req);
  const authService = inject(AuthService);
  return authService.getAccessToken().pipe(
    switchMap(token => {
      const request = req.clone({
        withCredentials: true,
        ...(token ? { setHeaders: { Authorization: `Bearer ${token}` } } : {}),
      });
      return next(request);
    }),
  );
};
