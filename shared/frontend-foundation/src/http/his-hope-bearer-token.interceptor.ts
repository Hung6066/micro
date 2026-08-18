import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from "@angular/common/http";
import { Observable, catchError, finalize, of, shareReplay, switchMap, take, throwError } from "rxjs";

export interface HisHopeBearerTokenOptions {
  /** Only requests matched by this predicate get an Authorization header. Defaults to any URL containing "/api/". */
  matches?: (url: string) => boolean;
  withCredentials?: boolean;
  /** Authorization scheme used for the access token. Defaults to Bearer. */
  tokenType?: "Bearer" | "DPoP";
  /** Refreshes the access token once after a 401, then retries the request once. */
  refreshAccessToken?: () => Observable<boolean>;
  /** Called when refresh is unavailable or has failed. */
  onSessionExpired?: () => void;
}

const defaultMatcher = (url: string): boolean => url.includes("/api/");

/** Builds an HttpInterceptorFn that attaches a bearer token from the app's own
 *  auth service. Centralizes the (previously copy-pasted per app) pattern of
 *  "skip non-API calls, fetch token, clone with Authorization header". */
export function createHisHopeBearerTokenInterceptor(
  getAccessToken: () => Observable<string>,
  options: HisHopeBearerTokenOptions = {},
): HttpInterceptorFn {
  const matches = options.matches ?? defaultMatcher;
  const withCredentials = options.withCredentials ?? true;
  const tokenType = options.tokenType ?? "Bearer";

  let refreshInFlight$: Observable<boolean> | undefined;

  const refreshOnce = (): Observable<boolean> => {
    if (!options.refreshAccessToken) return of(false);
    if (!refreshInFlight$) {
      refreshInFlight$ = options.refreshAccessToken().pipe(
        catchError(() => of(false)),
        finalize(() => { refreshInFlight$ = undefined; }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }
    return refreshInFlight$;
  };

  const sendWithToken = (request: HttpRequest<unknown>, retrying: boolean, next: HttpHandlerFn): Observable<HttpEvent<unknown>> =>
    // A token provider may be backed by a long-lived auth stream. Each HTTP
    // request needs one snapshot only; keeping the stream subscribed can keep
    // the HttpClient observable open and leave page-level loading flags stuck
    // after the response body has already arrived.
    getAccessToken().pipe(
      take(1),
      switchMap((token) => {
        const authenticatedRequest = request.clone({
          withCredentials,
          ...(token ? { setHeaders: { Authorization: `${tokenType} ${token}` } } : {}),
        });
        return next(authenticatedRequest).pipe(
          catchError((error: unknown) => {
            if (!(error instanceof HttpErrorResponse) || error.status !== 401 || retrying || !options.refreshAccessToken) {
              return throwError(() => error);
            }

            return refreshOnce().pipe(
              switchMap((refreshed) => refreshed
                ? sendWithToken(request, true, next)
                : throwError(() => error)),
              catchError((refreshError) => {
                options.onSessionExpired?.();
                return throwError(() => refreshError);
              }),
            );
          }),
        );
      }),
    );

  return (req, next) => {
    if (!matches(req.url)) {
      return next(withCredentials ? req.clone({ withCredentials: true }) : req);
    }
    return sendWithToken(req, false, next);
  };
}
