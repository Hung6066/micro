// @ts-nocheck
import { ErrorHandler, Injectable, Injector, NgZone } from '@angular/core';
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

  constructor(private injector: Injector, private ngZone: NgZone) {}

  handleError(error: unknown): void {
    const context = this.errorService.buildErrorContext(error);

    if (context.type !== 'UNKNOWN') {
      this.errorService.reportError(context).subscribe();
    }

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
