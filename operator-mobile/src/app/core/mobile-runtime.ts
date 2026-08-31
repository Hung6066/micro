import { Capacitor } from "@capacitor/core";
import {
  RuntimeConfigService,
  type HisHopeResolvedMobileRuntimeConfig,
} from "@his-hope/mobile-foundation";

function defaultMobileRuntimeSource() {
  const platform = Capacitor.isNativePlatform() ? Capacitor.getPlatform() : "web";
  const fallbackOrigin =
    platform === "android" ? "http://10.0.2.2:5000" : "http://localhost:5000";

  return (
    window.__HISHOPE_CONFIG__ ?? {
      apiOrigin: fallbackOrigin,
      oidcAuthority: fallbackOrigin,
      production: false,
    }
  );
}

export function createMobileRuntimeConfig(
  production: boolean,
): Readonly<HisHopeResolvedMobileRuntimeConfig> {
  const source = defaultMobileRuntimeSource();
  // A production Angular bundle must never downgrade to the HTTP emulator
  // origin when runtime configuration is missing or marked as development.
  // Local emulator installs must use the development configuration instead.
  const enforceProduction = production;
  return new RuntimeConfigService(source).require({
    platform: {
      isNative: Capacitor.isNativePlatform(),
      platform: Capacitor.isNativePlatform()
        ? (Capacitor.getPlatform() as "android" | "ios")
        : "web",
    },
    // Operator Mobile is tenant-bound to manufacturing; keep it separate from
    // the group-hq client used by the general mobile application.
    clientId: "his-hope-operator-mobile",
    scope: "openid profile email roles hishop:permissions offline_access",
    secureRoutes: ["/api/v1/"],
    redirectPath: "/auth/callback",
    postLogoutRedirectPath: "/auth/logout-callback",
    production: enforceProduction,
    webOrigin:
      typeof window !== "undefined" && window.location.origin
        ? window.location.origin
        : "http://localhost:4310",
  });
}

export function resolveMobileApiOrigin(): string {
  return createMobileRuntimeConfig(false).apiOrigin;
}

/** Production origin resolution — no emulator IPs, no http fallback. */
export function resolveProductionApiOrigin(): string {
  return createMobileRuntimeConfig(true).apiOrigin;
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

export function rewriteHisHopeNativeOidcUrl(
  url: string,
  publicAuthority: string,
): string {
  try {
    const parsed = new URL(url);
    const publicOrigin = new URL(publicAuthority);
    const dockerHosts = new Set(["identityservice", "identity", "his-hope-identity"]);
    const isDockerIdentity = dockerHosts.has(parsed.hostname);
    const isLocalOidcPort =
      (parsed.hostname === "localhost" || parsed.hostname === "127.0.0.1") &&
      (parsed.port === "5001" || parsed.port === "5003");
    if (!isDockerIdentity && !isLocalOidcPort) return url;
    parsed.protocol = publicOrigin.protocol;
    parsed.hostname = publicOrigin.hostname;
    parsed.port = publicOrigin.port;
    return parsed.toString();
  } catch {
    return url;
  }
}

export function resolveMobileRedirectUri(
  path: "/auth/callback" | "/auth/logout-callback",
): string {
  const runtime = createMobileRuntimeConfig(false);
  return path === "/auth/callback"
    ? runtime.redirectUrl
    : runtime.postLogoutRedirectUri;
}
