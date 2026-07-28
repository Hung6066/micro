import { Capacitor } from "@capacitor/core";

/** Injected by ops at deploy time (e.g. a config.js loaded before main.js) so the
 *  real API origin can change per environment without rebuilding the app bundle. */
declare global {
  interface Window {
    __HISHOPE_CONFIG__?: {
      apiOrigin?: string;
      sentryDsn?: string;
      sentryEnvironment?: string;
      pushNotificationsEnabled?: boolean;
    };
  }
}

// Must be replaced with the real production origin before a release build ships;
// left as a placeholder so a missing runtime config fails loudly instead of
// silently talking to an emulator/localhost.
const PRODUCTION_API_ORIGIN_FALLBACK = "https://api.his-hope.example";

export function resolveMobileApiOrigin(): string {
  if (Capacitor.isNativePlatform()) {
    if (Capacitor.getPlatform() === "android") return "http://10.0.2.2:5000";
    if (Capacitor.getPlatform() === "ios") return "http://localhost:5000";
  }

  if (
    typeof window !== "undefined" &&
    window.location.hostname &&
    window.location.hostname !== "localhost"
  ) {
    return `http://${window.location.hostname}:5000`;
  }

  return "http://localhost:5000";
}

/** Production origin resolution — no emulator IPs, no http fallback. */
export function resolveProductionApiOrigin(): string {
  const runtimeOrigin =
    typeof window !== "undefined"
      ? window.__HISHOPE_CONFIG__?.apiOrigin
      : undefined;
  return runtimeOrigin || PRODUCTION_API_ORIGIN_FALLBACK;
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
  if (Capacitor.isNativePlatform()) {
    return path === "/auth/callback"
      ? "hishope://auth/callback"
      : "hishope://auth/logout-callback";
  }

  const origin =
    typeof window !== "undefined" && window.location.origin
      ? window.location.origin
      : "http://localhost:4300";
  return `${origin}${path}`;
}
