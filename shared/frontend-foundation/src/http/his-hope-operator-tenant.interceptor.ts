import { HttpInterceptorFn } from "@angular/common/http";

export interface HisHopeOperatorTenantInterceptorOptions {
  /** Substring match against the request URL (e.g. `/api/v1/manufacturing`). */
  urlIncludes: string;
  getActiveTenantKey: () => string | null | undefined;
  /** When set, skip attaching the tenant context while active tenant equals home. */
  homeTenantKey?: string | null;
  /** @deprecated Kept for source compatibility; canonical requests never add tenantKey to URLs. */
  legacyQueryParam?: boolean;
}

/** Attach the canonical tenant context header for operator cross-tenant API calls. */
export function createHisHopeOperatorTenantInterceptor(
  options:
    | HisHopeOperatorTenantInterceptorOptions
    | (() => HisHopeOperatorTenantInterceptorOptions),
): HttpInterceptorFn {
  return (request, next) => {
    const resolved =
      typeof options === "function" ? options() : options;

    if (!request.url.includes(resolved.urlIncludes)) {
      return next(request);
    }

    const activeKey = resolved.getActiveTenantKey()?.trim();
    if (!activeKey) {
      return next(request);
    }

    const home = resolved.homeTenantKey?.trim();
    if (home && activeKey === home) {
      return next(request);
    }

    // Canonical context is carried only by the header. Remove a legacy query
    // value when present so requests cannot accidentally carry two selectors
    // or leak tenant identifiers into URLs.
    return next(
      request.clone({
        headers: request.headers.set("X-HisHope-Tenant", activeKey),
        params: request.params.delete("tenantKey"),
      }),
    );
  };
}
