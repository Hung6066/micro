import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthService } from './auth.service';
import { environment } from '@env/environment';

export interface ErrorContext {
  correlationId: string;
  message: string;
  type: string;
  stack?: string;
  url: string;
  userAction?: string;
  timestamp: string;
  userId?: string;
}

@Injectable({ providedIn: 'root' })
export class ErrorService {
  private currentAction = '';
  private readonly apiUrl = `${environment.apiUrl}/errors`;

  private http = inject(HttpClient);
  private authService = inject(AuthService);

  getCorrelationId(error: HttpErrorResponse): string {
    const fromHeaders = error.headers?.get('X-Correlation-Id');
    if (fromHeaders) return fromHeaders;

    if (typeof error.error === 'object' && error.error?.correlationId) {
      return error.error.correlationId;
    }

    return this.generateCorrelationId();
  }

  buildErrorContext(error: unknown): ErrorContext {
    const timestamp = new Date().toISOString();
    const user = this.authService['currentUserSubject']?.value;

    if (error instanceof HttpErrorResponse) {
      return {
        correlationId: this.getCorrelationId(error),
        message: this.getHttpErrorMessage(error),
        type: `HTTP_${error.status}`,
        url: error.url ?? window.location.href,
        userAction: this.currentAction || undefined,
        timestamp,
        userId: user?.id,
      };
    }

    if (error instanceof Error) {
      return {
        correlationId: this.generateCorrelationId(),
        message: error.message,
        type: error.name,
        stack: error.stack,
        url: window.location.href,
        userAction: this.currentAction || undefined,
        timestamp,
        userId: user?.id,
      };
    }

    return {
      correlationId: this.generateCorrelationId(),
      message: typeof error === 'string' ? error : 'An unknown error occurred',
      type: 'UNKNOWN',
      url: window.location.href,
      userAction: this.currentAction || undefined,
      timestamp,
      userId: user?.id,
    };
  }

  reportError(context: ErrorContext): Observable<void> {
    return this.http.post<void>(this.apiUrl, context).pipe(
      catchError(() => of(void 0)),
    );
  }

  trackUserAction(action: string): void {
    this.currentAction = action;
  }

  private getHttpErrorMessage(error: HttpErrorResponse): string {
    if (error.status === 0) {
      return 'Unable to connect to the server. Please check your connection.';
    }

    if (error.error?.error) return error.error.error;
    if (error.error?.message) return error.error.message;
    if (error.message) return error.message;

    return `HTTP error ${error.status}`;
  }

  private generateCorrelationId(): string {
    const timestamp = Date.now().toString(36);
    const random = Math.random().toString(36).substring(2, 8);
    return `hh-${timestamp}-${random}`;
  }
}
