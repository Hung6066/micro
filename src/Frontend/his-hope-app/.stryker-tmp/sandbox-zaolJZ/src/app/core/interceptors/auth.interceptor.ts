// @ts-nocheck
import { Injectable, Injector } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
} from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, throwError, BehaviorSubject, of } from 'rxjs';
import { catchError, filter, switchMap, take, tap, finalize } from 'rxjs/operators';
import { AuthService } from '@core/services/auth.service';

/**
 * Routes that should NEVER have the Authorization header attached
 * (e.g., auth endpoints that rely solely on HttpOnly cookies).
 */
const SKIP_AUTH_HEADER_URLS = [
  '/auth/login',
  '/auth/register',
  '/auth/forgot-password',
];

/**
 * Routes where 401 should NOT trigger a token refresh attempt
 * (auth endpoints that legitimately return 401).
 */
const SKIP_REFRESH_URLS = [
  '/auth/login',
  '/auth/register',
  '/auth/forgot-password',
  '/auth/verify',
  '/auth/refresh',
];

/**
 * Production-ready AuthInterceptor that:
 *
 * 1. Attaches JWT access token as `Authorization: Bearer <token>` on every
 *    outgoing request (except whitelisted auth URLs).
 * 2. On 401 responses, attempts a transparent token refresh using the
 *    HttpOnly refresh cookie.
 * 3. Queues concurrent requests during token refresh so only one refresh
 *    call is made at a time.
 * 4. Adds a correlation ID header (`X-Correlation-ID`) and request timing
 *    for observability.
 * 5. Redirects to login when refresh fails.
 */
@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private isRefreshing = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);
  private readonly correlationIdPrefix = 'hh-';

  constructor(
    private injector: Injector,
    private router: Router,
  ) {}

  intercept(
    req: HttpRequest<unknown>,
    next: HttpHandler,
  ): Observable<HttpEvent<unknown>> {
    const authService = this.injector.get(AuthService);

    // Decorate request with observability headers
    const correlationId = this.generateCorrelationId();
    const startTime = performance.now();
    let decoratedReq = req.clone({
      setHeaders: {
        'X-Correlation-ID': correlationId,
      },
    });

    // Attach Bearer token unless this is a skip-whitelisted URL
    const shouldSkipToken = SKIP_AUTH_HEADER_URLS.some((url) =>
      decoratedReq.url.includes(url),
    );

    if (!shouldSkipToken) {
      const token = authService.getStoredAccessToken();
      if (token) {
        decoratedReq = decoratedReq.clone({
          setHeaders: {
            Authorization: `Bearer ${token}`,
          },
        });
      }
    }

    return next.handle(decoratedReq).pipe(
      tap({
        complete: () => {
          const elapsed = performance.now() - startTime;
          if (elapsed > 1000) {
            console.warn(
              `[AuthInterceptor] Slow request [${correlationId}]: ${decoratedReq.method} ${decoratedReq.url} took ${elapsed.toFixed(0)}ms`,
            );
          }
        },
      }),
      catchError((error: HttpErrorResponse) => {
        // Only attempt refresh on 401 for non-auth URLs
        if (
          error.status === 401 &&
          !SKIP_REFRESH_URLS.some((url) => decoratedReq.url.includes(url))
        ) {
          return this.handle401(decoratedReq, next, authService);
        }

        return throwError(() => error);
      }),
    );
  }

  /**
   * Handles 401 responses by attempting a token refresh.
   * Uses a queue pattern: concurrent 401s wait for a single refresh call.
   */
  private handle401(
    req: HttpRequest<unknown>,
    next: HttpHandler,
    authService: AuthService,
  ): Observable<HttpEvent<unknown>> {
    if (!this.isRefreshing) {
      this.isRefreshing = true;
      this.refreshTokenSubject.next(null);

      return authService.refreshToken().pipe(
        switchMap((user) => {
          // Login and refreshToken responses may include an accessToken
          // that should be stored for subsequent requests.
          this.isRefreshing = false;
          this.refreshTokenSubject.next('refreshed');

          // Re-attach the (new) token and retry the original request
          const token = authService.getStoredAccessToken();
          const retryReq = token
            ? req.clone({
                setHeaders: {
                  Authorization: `Bearer ${token}`,
                },
              })
            : req;

          return next.handle(retryReq);
        }),
        catchError((refreshError) => {
          this.isRefreshing = false;
          this.refreshTokenSubject.next(null);
          authService.clearStoredAccessToken();
          this.router.navigate(['/auth/login']);
          return throwError(() => refreshError);
        }),
        finalize(() => {
          this.isRefreshing = false;
        }),
      );
    }

    // Another refresh is already in progress — queue this request
    return this.refreshTokenSubject.pipe(
      filter((token) => token !== null),
      take(1),
      switchMap(() => {
        const token = authService.getStoredAccessToken();
        const retryReq = token
          ? req.clone({
              setHeaders: {
                Authorization: `Bearer ${token}`,
              },
            })
          : req;
        return next.handle(retryReq);
      }),
    );
  }

  /**
   * Generate a unique correlation ID for observability.
   * Format: `hh-<timestamp>-<random-hex>`
   */
  private generateCorrelationId(): string {
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `${this.correlationIdPrefix}${timestamp}-${random}`;
  }
}
