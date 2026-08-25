import { HttpParams } from "@angular/common/http";

/** Explicit scope override (e.g. environment scope). Omit to use tenant interceptor. */
export function optionalScopeIdParams(
  scopeId?: string,
): { params: HttpParams } | undefined {
  if (!scopeId) {
    return undefined;
  }

  return { params: new HttpParams().set("scopeId", scopeId) };
}
