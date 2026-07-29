import { HttpErrorResponse, HttpHandlerFn, HttpHeaders, HttpInterceptorFn, HttpRequest, HttpResponse } from "@angular/common/http";
import { inject } from "@angular/core";
import { Capacitor } from "@capacitor/core";
import { from, of, switchMap, throwError } from "rxjs";
import { MobilePlatformService } from "./mobile-platform.service";
import { DpopProofService } from "./dpop-proof.service";

// Capacitor serves the WebView from https://localhost. Android development
// therefore cannot call the local HTTP gateway from WebView because Chromium
// blocks mixed content. Use the native client for Android HTTP dev traffic and
// for every pinned iOS HTTPS request. Production Android must use HTTPS too.
function isNativeTransportRequest(url: string): boolean {
  try {
    const protocol = new URL(url).protocol;
    return (Capacitor.getPlatform() === "android" && protocol === "http:") ||
      (Capacitor.getPlatform() === "ios" && protocol === "https:");
  } catch {
    return false;
  }
}

function isDpopTokenRequest(url: string): boolean {
  try {
    return new URL(url).pathname.endsWith("/connect/token");
  } catch {
    return false;
  }
}

function serializeNativeBody(request: HttpRequest<unknown>): string | undefined {
  const body = request.serializeBody();
  if (body === null) return undefined;
  if (typeof body === "string") return body;
  if (body instanceof URLSearchParams) return body.toString();
  if (body instanceof ArrayBuffer) return new TextDecoder().decode(body);
  throw new Error("Native pinned HTTP supports only text, JSON, and URL-encoded request bodies.");
}

export const mobileNativeHttpInterceptor: HttpInterceptorFn = (request: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const platform = inject(MobilePlatformService);
  const dpop = inject(DpopProofService);

  const authorization = request.headers.get("Authorization") ?? "";
  const accessToken = authorization.match(/^DPoP\s+(.+)$/i)?.[1];

  // Browser preview uses the normal transport, but every DPoP-bound request
  // still needs a proof. Requests without a token only pass through when they
  // are not the authorization-code exchange.
  if (!Capacitor.isNativePlatform() && !isDpopTokenRequest(request.urlWithParams) && !accessToken) return next(request);

  if (!isNativeTransportRequest(request.urlWithParams)) {
    // The authorization-code exchange has no access token yet, but the
    // mobile client is sender-constrained and the token endpoint still
    // requires a DPoP proof. This also keeps browser preview aligned with
    // the native login contract.
    if (!accessToken && !isDpopTokenRequest(request.urlWithParams)) return next(request);
    return from(dpop.createProof(request.urlWithParams, request.method, accessToken)).pipe(
      switchMap(proof => next(request.clone({ setHeaders: { DPoP: proof } }))),
    );
  }

  const headers: Record<string, string> = {};
  request.headers.keys().forEach(key => {
    const value = request.headers.get(key);
    if (value !== null) headers[key] = value;
  });
  const body = serializeNativeBody(request);

  const proof$ = isDpopTokenRequest(request.urlWithParams) || accessToken
    ? from(dpop.createProof(request.urlWithParams, request.method, accessToken))
    : of<string | null>(null);

  return proof$.pipe(
    switchMap(proof => {
      if (proof) headers["DPoP"] = proof;
      return from(platform.nativeRequest({ url: request.urlWithParams, method: request.method, headers, body }));
    }),
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
