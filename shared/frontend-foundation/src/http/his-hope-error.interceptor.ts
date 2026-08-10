import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { Observable, throwError, timer } from "rxjs";
import { catchError, retry } from "rxjs/operators";
import { HisHopeErrorReportingService } from "./his-hope-error-reporting.service";

const RETRYABLE_STATUSES = new Set([0, 408, 429, 502, 503, 504]);
const RETRYABLE_METHODS = new Set(["GET", "HEAD"]);
const MAX_RETRIES = 2;

function isRetryable(req: { method: string }, error: unknown): boolean {
  if (!RETRYABLE_METHODS.has(req.method)) return false;
  return (
    error instanceof HttpErrorResponse && RETRYABLE_STATUSES.has(error.status)
  );
}

/** Retries idempotent requests on transient failures with jittered backoff,
 *  reports every terminal failure (with correlation id) to
 *  `HisHopeErrorReportingService`, then rethrows so page-level `catchError`
 *  handlers keep controlling the user-facing message. */
export const hisHopeErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const errorReporting = inject(HisHopeErrorReportingService);

  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error, attempt) => {
        if (!isRetryable(req, error)) return throwError(() => error);
        const backoffMs =
          Math.min(4000, 250 * 2 ** attempt) + Math.random() * 100;
        return timer(backoffMs);
      },
    }),
    catchError((error: unknown): Observable<never> => {
      if (error instanceof HttpErrorResponse) {
        errorReporting.report({
          message: error.message,
          severity:
            error.status >= 500 || error.status === 0 ? "error" : "warning",
          correlationId: req.headers.get("X-Correlation-Id") ?? undefined,
          statusCode: error.status,
          url: req.urlWithParams,
        });
      }
      return throwError(() => error);
    }),
  );
};
