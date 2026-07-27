import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { switchMap } from 'rxjs/operators';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/')) {
    return next(req.clone({ withCredentials: true }));
  }

  const authService = inject(AuthService);
  return authService.getAccessToken().pipe(
    switchMap(token => {
      const request = token
        ? req.clone({
            withCredentials: true,
            setHeaders: { Authorization: `Bearer ${token}` },
          })
        : req.clone({ withCredentials: true });
      return next(request);
    }),
  );
};
