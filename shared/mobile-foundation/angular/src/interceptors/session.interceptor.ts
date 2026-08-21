import { HttpErrorResponse, HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, throwError } from "rxjs";
import { HisHopeMobileSessionService } from "../services/session.service";

/** Surfaces the session-expired dialog when the API rejects a request with 401. */
export const hisHopeMobileSessionInterceptor: HttpInterceptorFn = (
  request,
  next,
) => {
  const session = inject(HisHopeMobileSessionService);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        session.handleUnauthorized();
      }
      return throwError(() => error);
    }),
  );
};
