import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap, take } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

/**
 * Auth interceptor for dual-mode authentication:
 * - OIDC tokens (via angular-auth-oidc-client): adds Bearer header
 * - BFF sessions (via HttpOnly cookies): handled automatically by withCredentials
 *
 * Both modes coexist during the transition from OIDC to BFF-only.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.includes('/api/')) {
    return next(req);
  }

  const authService = inject(AuthService);

  return authService.getAccessToken().pipe(
    take(1),
    switchMap((token) => {
      if (!token) {
        return next(req);
      }

      return next(req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`,
        },
      }));
    }),
  );
};
