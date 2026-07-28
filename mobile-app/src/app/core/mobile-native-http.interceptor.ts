import { HttpErrorResponse, HttpHandlerFn, HttpHeaders, HttpInterceptorFn, HttpRequest, HttpResponse } from "@angular/common/http";
import { inject } from "@angular/core";
import { Capacitor } from "@capacitor/core";
import { from, switchMap, throwError } from "rxjs";
import { MobilePlatformService } from "./mobile-platform.service";
import { environment } from "../../environments/environment";

// OIDC discovery/token endpoints live beside `/api/v1`, so matching only the
// API path lets HTTPS WebView requests fall through and get blocked as mixed
// content when the local emulator backend uses HTTP.
const apiOrigin = environment.oidc.authority.replace(/\/$/, "");

export const mobileNativeHttpInterceptor: HttpInterceptorFn = (request: HttpRequest<unknown>, next: HttpHandlerFn) => {
  if (!Capacitor.isNativePlatform() || !request.url.startsWith(apiOrigin)) return next(request);

  const platform = inject(MobilePlatformService);
  const headers: Record<string, string> = {};
  request.headers.keys().forEach(key => {
    const value = request.headers.get(key);
    if (value !== null) headers[key] = value;
  });
  const body = request.body === null || request.body === undefined
    ? undefined
    : typeof request.body === "string" ? request.body : JSON.stringify(request.body);

  return from(platform.nativeRequest({ url: request.urlWithParams, method: request.method, headers, body })).pipe(
    switchMap(response => {
      let bodyValue: unknown = response.body;
      const contentType = response.headers["content-type"] ?? response.headers["Content-Type"] ?? "";
      if (response.body && contentType.includes("json")) {
        try { bodyValue = JSON.parse(response.body); } catch { bodyValue = response.body; }
      }
      const result = new HttpResponse({ body: bodyValue, status: response.status, headers: new HttpHeaders(response.headers), url: request.urlWithParams });
      if (response.status >= 400) {
        return throwError(() => new HttpErrorResponse({ error: bodyValue, status: response.status, url: request.urlWithParams }));
      }
      return [result];
    }),
  );
};
