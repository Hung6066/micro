import {
  resolveMobileRedirectUri,
  resolveProductionApiOrigin,
  resolveMobileSentryDsn,
  resolveMobileSentryEnvironment,
  resolveMobilePushNotificationsEnabled,
} from "../app/core/mobile-runtime";

const apiOrigin = resolveProductionApiOrigin();

export const environment = {
  production: true,
  adminApiUrl: `${apiOrigin}/api/v1/admin`,
  oidc: {
    authority: apiOrigin,
    clientId: "his-hope-mobile",
    redirectUrl: resolveMobileRedirectUri("/auth/callback"),
    postLogoutRedirectUri: resolveMobileRedirectUri("/auth/logout-callback"),
    scope: "openid profile email roles hishop:permissions offline_access",
    secureRoutes: ["/api/v1/"],
  },
  appVersion: "0.1.0",
  // Configure this with the production GlitchTip project DSN at release time.
  sentryDsn: resolveMobileSentryDsn(""),
  sentryEnvironment: resolveMobileSentryEnvironment("production"),
  pushNotificationsEnabled: resolveMobilePushNotificationsEnabled(true),
  security: {
    // CI must fail the release if this placeholder is still present.
    certificatePins: [{ host: "api.his-hope.example", sha256Spki: "sha256/REPLACE_IN_RELEASE" }],
  },
};
