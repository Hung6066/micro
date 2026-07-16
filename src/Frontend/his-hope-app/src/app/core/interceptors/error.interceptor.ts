import { Injectable, NgZone } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
} from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private readonly SKIP_NOTIFICATION_URLS = ['/auth/verify', '/auth/me'];

  constructor(
    private router: Router,
    private snackBar: MatSnackBar,
    private ngZone: NgZone,
  ) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        const shouldNotify = !this.SKIP_NOTIFICATION_URLS.some((url) =>
          req.url.includes(url),
        );

        // Network error / no response
        if (error.status === 0) {
          this.showNotification(
            'Network error: Unable to connect to the server. Please check your connection.',
          );
          return throwError(() => error);
        }

        switch (error.status) {
          case 401: {
            // 401 handling is done by AuthInterceptor (token refresh).
            // Only show a notification for non-auth endpoints that slip through.
            if (shouldNotify) {
              this.showNotification('Session expired. Please log in again.');
            }
            break;
          }
          case 403: {
            if (shouldNotify) {
              this.showNotification(
                'Access denied. You do not have permission to perform this action.',
              );
            }
            break;
          }
          case 500: {
            if (shouldNotify) {
              this.showNotification(
                'Server error: Something went wrong. Please try again later.',
              );
            }
            break;
          }
          default: {
            if (shouldNotify && error.status && error.status >= 400) {
              const message =
                error.error?.error ||
                error.error?.message ||
                error.message ||
                'An unexpected error occurred';
              this.showNotification(message);
            }
            break;
          }
        }

        return throwError(() => error);
      }),
    );
  }

  private showNotification(message: string): void {
    this.ngZone.run(() => {
      this.snackBar.open(message, 'Close', {
        duration: 5000,
        panelClass: ['error-snackbar'],
      });
    });
  }
}
