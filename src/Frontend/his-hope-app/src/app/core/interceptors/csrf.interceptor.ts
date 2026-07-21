import { HttpInterceptorFn } from '@angular/common/http';

export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(req.method)) {
    const csrfToken = getCsrfCookie();
    if (csrfToken) {
      req = req.clone({ setHeaders: { 'X-CSRF-Token': csrfToken } });
    }
  }

  if (req.url.startsWith('/api/v1/') && !req.withCredentials) {
    req = req.clone({ withCredentials: true });
  }

  return next(req);
};

function getCsrfCookie(): string | null {
  const match = document.cookie.match(/(?:^|; )hishop_csrf=([^;]*)/);
  return match ? decodeURIComponent(match[1]) : null;
}
