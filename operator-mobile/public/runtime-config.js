// Deployment-owned runtime configuration. Replace values per environment.
// This file is loaded before Angular and is intentionally not a secret store:
// a GlitchTip DSN is a public project identifier, not an access credential.
window.__HISHOPE_CONFIG__ = {
  environment: window.__HISHOPE_CONFIG__?.environment || "development",
  contractVersion: window.__HISHOPE_CONFIG__?.contractVersion || "1",
  // Use platform-aware local defaults when deployment does not inject API origin.
  // Android emulator reaches host via 10.0.2.2, while iOS simulator uses localhost.
  apiOrigin:
    window.__HISHOPE_CONFIG__?.apiOrigin ||
    ((window.Capacitor?.getPlatform?.() === "android")
      ? "http://10.0.2.2:5000"
      : "http://localhost:5000"),
  oidcAuthority:
    window.__HISHOPE_CONFIG__?.oidcAuthority ||
    window.__HISHOPE_CONFIG__?.apiOrigin ||
    ((window.Capacitor?.getPlatform?.() === "android")
      ? "http://10.0.2.2:5000"
      : "http://localhost:5000"),
  production: window.__HISHOPE_CONFIG__?.production ?? false,
  // This build ships google-services.json and GoogleService-Info.plist.
  // Deployments without push credentials can explicitly override this with
  // false through their injected runtime configuration.
  pushNotificationsEnabled:
    window.__HISHOPE_CONFIG__?.pushNotificationsEnabled ?? true,
  sentryDsn: window.__HISHOPE_CONFIG__?.sentryDsn || "http://29fc4eb7be884d6ea155347458a8d93b@localhost:8000/1",
  sentryEnvironment: window.__HISHOPE_CONFIG__?.sentryEnvironment || "production",
};
