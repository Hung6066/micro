import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { throwError } from 'rxjs';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/** BFF-only browser contract: cookies plus CSRF, never an Authorization header. */
export const hisHopeCookieSessionInterceptor: HttpInterceptorFn = (req, next) => {
  if (
    MUTATING_METHODS.has(req.method) &&
    req.url.includes('/api/') &&
    // Session exchange is the bootstrap mutation that establishes the CSRF
    // cookie; all later state-changing API calls still require the token.
    !req.url.includes('/auth/session/exchange') &&
    !readCookie('hishop_csrf')
  ) {
    return throwError(
      () =>
        new HttpErrorResponse({
          status: 403,
          statusText: 'CSRF token missing',
          url: req.urlWithParams,
          error: {
            detail: 'Session expired or CSRF cookie missing. Sign in again.',
          },
        }),
    );
  }

  let request = req.clone({ withCredentials: true });
  if (MUTATING_METHODS.has(req.method)) {
    const csrfToken = readCookie('hishop_csrf');
    if (csrfToken) {
      request = request.clone({ setHeaders: { 'X-CSRF-Token': csrfToken } });
    }
  }
  return next(request);
};

function readCookie(name: string): string | null {
  if (typeof document === 'undefined') return null;
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}
