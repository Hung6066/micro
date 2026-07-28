// Deployment-owned runtime configuration. Replace values per environment.
// This file is loaded before Angular and is intentionally not a secret store:
// a GlitchTip DSN is a public project identifier, not an access credential.
window.__HISHOPE_CONFIG__ = {
  // Local Capacitor builds run inside the Android emulator. Release
  // deployments must replace this value with the real HTTPS API origin.
  apiOrigin:
    window.__HISHOPE_CONFIG__?.apiOrigin || "http://10.0.2.2:5000",
  // Local emulator builds do not include Firebase services. Set this to true
  // only in a deployment that ships google-services.json/APNs configuration.
  pushNotificationsEnabled:
    window.__HISHOPE_CONFIG__?.pushNotificationsEnabled ?? false,
  sentryDsn: window.__HISHOPE_CONFIG__?.sentryDsn || "http://29fc4eb7be884d6ea155347458a8d93b@localhost:8000/1",
  sentryEnvironment: window.__HISHOPE_CONFIG__?.sentryEnvironment || "production",
};
