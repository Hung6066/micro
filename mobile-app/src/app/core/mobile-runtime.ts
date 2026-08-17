import { Capacitor } from "@capacitor/core";
import { RuntimeConfigService } from "../../../../shared/mobile-foundation/src/runtime/runtime-config.service";

function defaultMobileRuntimeSource() {
  const platform = Capacitor.isNativePlatform() ? Capacitor.getPlatform() : "web";
  const fallbackOrigin =
    platform === "android"
      ? "http://10.0.2.2:5000"
      : "http://localhost:5000";

  return (
    window.__HISHOPE_CONFIG__ ?? {
      apiOrigin: fallbackOrigin,
      oidcAuthority: fallbackOrigin,
      production: false,
    }
  );
}

function requireMobileRuntime(production: boolean) {
  return new RuntimeConfigService(defaultMobileRuntimeSource()).require({
    platform: {
      isNative: Capacitor.isNativePlatform(),
      platform: Capacitor.isNativePlatform()
        ? (Capacitor.getPlatform() as "android" | "ios")
        : "web",
    },
    clientId: "his-hope-mobile",
    scope: "openid profile email roles hishop:permissions offline_access",
    secureRoutes: ["/api/v1/"],
    redirectPath: "/auth/callback",
    postLogoutRedirectPath: "/auth/logout-callback",
    production,
    webOrigin:
      typeof window !== "undefined" && window.location.origin
        ? window.location.origin
        : "http://localhost:4300",
  });
}

export function resolveMobileApiOrigin(): string {
  return requireMobileRuntime(false).apiOrigin;
}

/** Production origin resolution — no emulator IPs, no http fallback. */
export function resolveProductionApiOrigin(): string {
  return requireMobileRuntime(true).apiOrigin;
}

export function resolveMobileSentryDsn(defaultDsn = ""): string {
  if (typeof window === "undefined") return defaultDsn;
  return window.__HISHOPE_CONFIG__?.sentryDsn?.trim() || defaultDsn;
}

export function resolveMobileSentryEnvironment(defaultEnvironment: string): string {
  if (typeof window === "undefined") return defaultEnvironment;
  return (
    window.__HISHOPE_CONFIG__?.sentryEnvironment?.trim() || defaultEnvironment
  );
}

export function resolveMobilePushNotificationsEnabled(defaultEnabled: boolean): boolean {
  if (typeof window === "undefined") return defaultEnabled;
  return window.__HISHOPE_CONFIG__?.pushNotificationsEnabled ?? defaultEnabled;
}

export function resolveMobileRedirectUri(
  path: "/auth/callback" | "/auth/logout-callback",
): string {
  const runtime = requireMobileRuntime(false);
  return path === "/auth/callback"
    ? runtime.redirectUrl
    : runtime.postLogoutRedirectUri;
}
