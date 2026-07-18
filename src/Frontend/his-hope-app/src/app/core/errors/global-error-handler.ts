import { ErrorHandler, inject, Injectable, Injector, NgZone } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { ErrorService } from '@core/services/error.service';
import { Store } from '@ngrx/store';
import { captureError, clearError } from '@store/error/error.actions';

interface ErrorDisplay {
  message: string;
  panelClass: string;
  duration: number;
}

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  /** Minimum interval (ms) between error reports to avoid flooding the errors API. */
  private static readonly REPORT_THROTTLE_MS = 2000;

  /** Tracks last report timestamp per unique error type + URL combo. */
  private readonly lastReportedAt = new Map<string, number>();

  private get errorService(): ErrorService {
    return this.injector.get(ErrorService);
  }

  private get snackBar(): MatSnackBar {
    return this.injector.get(MatSnackBar);
  }

  private get store(): Store | null {
    try {
      return this.injector.get(Store);
    } catch {
      return null;
    }
  }

  private injector = inject(Injector);
  private ngZone = inject(NgZone);

  handleError(error: unknown): void {
    const context = this.errorService.buildErrorContext(error);

    // === Guard: skip reporting if this error originated from the errors API ===
    const isFromErrorsApi = context.url?.includes('/api/v1/errors');
    if (isFromErrorsApi) {
      // Still show user feedback for the 429 / errors endpoint failure
      this.showUserFeedback(error, context);
      return;
    }

    // === Throttle: prevent rapid-fire reporting ===
    const throttleKey = `${context.type}::${context.url}`;
    const now = Date.now();
    const lastReport = this.lastReportedAt.get(throttleKey) ?? 0;

    if (now - lastReport >= GlobalErrorHandler.REPORT_THROTTLE_MS) {
      this.lastReportedAt.set(throttleKey, now);
      this.errorService.reportError(context).subscribe();
    }

    this.showUserFeedback(error, context);
  }

  private showUserFeedback(error: unknown, context: import('@core/services/error.service').ErrorContext): void {
    this.ngZone.run(() => {
      const display = this.getUserMessage(error);

      this.snackBar.open(display.message, 'OK', {
        duration: display.duration,
        panelClass: [display.panelClass],
      });

      if (this.store) {
        this.store.dispatch(captureError({
          message: context.message,
          code: context.type,
          correlationId: context.correlationId,
        }));

        setTimeout(() => {
          this.store?.dispatch(clearError());
        }, 8000);
      }
    });
  }

  private getUserMessage(error: unknown): ErrorDisplay {
    if (error instanceof HttpErrorResponse) {
      switch (error.status) {
        case 0:
          return {
            message: 'Unable to connect to the server. Please check your connection.',
            panelClass: 'error-snackbar-critical',
            duration: 0,
          };
        case 400:
          return {
            message: 'Invalid request. Please check your input.',
            panelClass: 'error-snackbar',
            duration: 5000,
          };
        case 403:
          return {
            message: 'You do not have permission to perform this action.',
            panelClass: 'error-snackbar',
            duration: 5000,
          };
        case 404:
          return {
            message: 'The requested resource was not found.',
            panelClass: 'error-snackbar',
            duration: 5000,
          };
        case 422:
          return {
            message: 'Validation failed. Please check your input.',
            panelClass: 'error-snackbar',
            duration: 5000,
          };
        case 429:
          return {
            message: 'Too many requests. Please try again later.',
            panelClass: 'error-snackbar',
            duration: 8000,
          };
        default:
          if (error.status >= 500) {
            return {
              message: 'A server error occurred. Please try again later.',
              panelClass: 'error-snackbar-critical',
              duration: 0,
            };
          }
      }
    }

    if (error instanceof TypeError) {
      return {
        message: 'An unexpected application error occurred. Please refresh the page.',
        panelClass: 'error-snackbar-critical',
        duration: 0,
      };
    }

    return {
      message: 'An unexpected error occurred. Please try again.',
      panelClass: 'error-snackbar',
      duration: 5000,
    };
  }
}
