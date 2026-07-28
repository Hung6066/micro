import { HttpInterceptorFn } from "@angular/common/http";
import { Observable, switchMap } from "rxjs";

export interface HisHopeBearerTokenOptions {
  /** Only requests matched by this predicate get an Authorization header. Defaults to any URL containing "/api/". */
  matches?: (url: string) => boolean;
  withCredentials?: boolean;
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

  return (req, next) => {
    if (!matches(req.url)) {
      return next(withCredentials ? req.clone({ withCredentials: true }) : req);
    }
    return getAccessToken().pipe(
      switchMap((token) => {
        const request = req.clone({
          withCredentials,
          ...(token
            ? { setHeaders: { Authorization: `Bearer ${token}` } }
            : {}),
        });
        return next(request);
      }),
    );
  };
}
