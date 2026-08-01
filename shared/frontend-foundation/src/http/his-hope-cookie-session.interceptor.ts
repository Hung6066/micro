import { HttpInterceptorFn } from '@angular/common/http';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/** BFF-only browser contract: cookies plus CSRF, never an Authorization header. */
export const hisHopeCookieSessionInterceptor: HttpInterceptorFn = (req, next) => {
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
