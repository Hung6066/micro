import { inject, Injectable, NgZone, Injector } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, retry, take } from 'rxjs/operators';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '@core/services/auth.service';
import { AuditService } from '@core/services/audit.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly SKIP_NOTIFICATION_URLS = [
    '/auth/verify', '/auth/me', '/api/v1/errors', '/api/v1/audit/events',
  ];
  private readonly TRANSIENT_STATUSES = [503, 504];
  private readonly MAX_RETRIES = 1;

  private injector = inject(Injector);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);
  private ngZone = inject(NgZone);
  private auditService = inject(AuditService);

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      retry({
        count: this.MAX_RETRIES,
        delay: (error: HttpErrorResponse) => {
          if (this.TRANSIENT_STATUSES.includes(error.status)) {
            return of(true);
          }
          return throwError(() => error);
        },
      }),
      catchError((error: HttpErrorResponse) => {
        if (!this.isSkippableUrl(req.url)) {
          // Audit log (full context gửi backend an toàn)
          this.auditService.log('error.server', {
            status: error.status,
            url: req.url,
            method: req.method,
            correlationId: error.headers?.get('X-Correlation-ID') || undefined,
          });
        }

        const shouldNotify = !this.isSkippableUrl(req.url);

        if (error.status === 0) {
          this.showNotification(
            'Network error: Unable to connect to the server. Please check your connection.',
            'error-snackbar-critical',
            false,
          );
          return throwError(() => error);
        }

        const authService = this.injector.get(AuthService);

        switch (error.status) {
          case 401: {
            if (!req.url.includes('/auth/') && !req.url.includes('/dashboard/') && !req.url.includes('/patients/search')) {
              authService.clearStoredAccessToken();
              authService.isAuthenticated().pipe(take(1)).subscribe(isAuth => {
                if (isAuth) {
                  this.router.navigate(['/auth/login'], { queryParams: { reason: 'session_expired' } });
                } else {
                  this.router.navigate(['/auth/login']);
                }
              });
            }
            break;
          }
          case 403: {
            if (shouldNotify) {
              this.showNotification(
                'Access denied. You do not have permission to perform this action.',
                'error-snackbar',
                true,
              );
            }
            break;
          }
          case 422: {
            // Sanitize: không lộ raw error.message từ server
            if (shouldNotify) {
              this.showNotification('Validation failed. Please check your input.', 'error-snackbar', true);
            }
            break;
          }
          case 429: {
            if (shouldNotify) {
              this.showNotification(
                'Too many requests. Please wait before trying again.',
                'error-snackbar',
                true,
              );
            }
            break;
          }
          default: {
            if (error.status >= 500) {
              if (shouldNotify) {
                this.showNotification(
                  'A server error occurred. Please try again later.',
                  'error-snackbar-critical',
                  false,
                );
              }
            } else if (shouldNotify && error.status && error.status >= 400) {
              this.showNotification('An unexpected error occurred.', 'error-snackbar', true);
            }
            break;
          }
        }

        return throwError(() => error);
      }),
    );
  }

  private isSkippableUrl(url: string): boolean {
    return this.SKIP_NOTIFICATION_URLS.some((skip) => url.includes(skip));
  }

  private showNotification(message: string, panelClass: string, autoDismiss: boolean): void {
    this.ngZone.run(() => {
      this.snackBar.open(message, 'Close', {
        duration: autoDismiss ? 5000 : undefined,
        panelClass: [panelClass],
      });
    });
  }
}
