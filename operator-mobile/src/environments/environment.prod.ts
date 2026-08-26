import {
  createMobileRuntimeConfig,
  resolveMobilePushNotificationsEnabled,
  resolveMobileSentryDsn,
  resolveMobileSentryEnvironment,
} from "../app/core/mobile-runtime";

const runtime = createMobileRuntimeConfig(true);

export const environment = {
  production: true,
  adminApiUrl: runtime.adminApiUrl,
  oidc: {
    authority: runtime.oidcAuthority,
    clientId: runtime.clientId,
    redirectUrl: runtime.redirectUrl,
    postLogoutRedirectUri: runtime.postLogoutRedirectUri,
    scope: runtime.scope,
    secureRoutes: runtime.secureRoutes,
  },
  appVersion: runtime.appVersion,
  sentryDsn: resolveMobileSentryDsn(""),
  sentryEnvironment: resolveMobileSentryEnvironment("production"),
  pushNotificationsEnabled: resolveMobilePushNotificationsEnabled(true),
  security: {
    certificatePins: [{ host: "api.his-hope.example", sha256Spki: "sha256/REPLACE_IN_RELEASE" }],
  },
};
