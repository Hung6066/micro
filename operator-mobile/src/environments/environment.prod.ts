import {
  createMobileRuntimeConfig,
  resolveMobilePushNotificationsEnabled,
  resolveMobileSentryDsn,
  resolveMobileSentryEnvironment,
} from "../app/core/mobile-runtime";

const runtime = createMobileRuntimeConfig(true);

export const environment = {
  production: true,
  homeTenantKey: "manufacturing",
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
    // Replaced by scripts/prepare-mobile-release.mjs from the protected
    // release environment. Production must never ship an empty allow-list.
    certificatePins: [{ host: "api.his-hope.example", sha256Spki: "sha256/REPLACE_IN_RELEASE" }],
  },
};
