import { createMobileRuntimeConfig, resolveMobilePushNotificationsEnabled, resolveMobileSentryDsn, resolveMobileSentryEnvironment } from '../app/core/mobile-runtime';

const runtime = createMobileRuntimeConfig(false);

export const environment = {
  production: false,
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
  sentryDsn: resolveMobileSentryDsn(''),
  sentryEnvironment: resolveMobileSentryEnvironment('development'),
  pushNotificationsEnabled: resolveMobilePushNotificationsEnabled(true),
  security: { certificatePins: [] },
};
