import { HttpInterceptorFn } from "@angular/common/http";

const HEADER = "X-Correlation-Id";

function generateId(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto)
    return crypto.randomUUID();
  return `hh-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Stamps every outgoing request with a correlation id so client errors can be
 *  matched to server-side logs/traces. Skips requests that already carry one
 *  (e.g. retries that should keep the original id). */
export const hisHopeCorrelationIdInterceptor: HttpInterceptorFn = (
  req,
  next,
) => {
  if (req.headers.has(HEADER)) return next(req);
  return next(req.clone({ setHeaders: { [HEADER]: generateId() } }));
};
