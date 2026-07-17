import { Injectable, NgZone, Injector } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
} from '@angular/common/http';
import { Observable, throwError, of } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ErrorService } from '@core/services/error.service';
import { AuthService } from '@core/services/auth.service';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly SKIP_NOTIFICATION_URLS = ['/auth/verify', '/auth/me', '/api/v1/errors'];
  private readonly TRANSIENT_STATUSES = [503, 504];
  private readonly MAX_RETRIES = 1;

  constructor(
    private injector: Injector,
    private router: Router,
    private snackBar: MatSnackBar,
    private ngZone: NgZone,
  ) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    const errorService = this.injector.get(ErrorService);

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
        const correlationId = errorService.getCorrelationId(error);
        const shouldNotify = !this.SKIP_NOTIFICATION_URLS.some((url) =>
          req.url.includes(url),
        );

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
            if (!req.url.includes('/auth/')) {
              authService.clearStoredAccessToken();
              this.router.navigate(['/auth/login']);
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
            const message = error.error?.error || 'Validation failed. Please check your input.';
            if (shouldNotify) {
              this.showNotification(message, 'error-snackbar', true);
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
              const message = correlationId
                ? `Server error. Reference: ${correlationId}`
                : 'A server error occurred. Please try again later.';
              if (shouldNotify) {
                this.showNotification(
                  message,
                  'error-snackbar-critical',
                  false,
                );
              }
            } else if (shouldNotify && error.status && error.status >= 400) {
              const message =
                error.error?.error ||
                error.error?.message ||
                error.message ||
                'An unexpected error occurred';
              this.showNotification(message, 'error-snackbar', true);
            }
            break;
          }
        }

        return throwError(() => error);
      }),
    );
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
