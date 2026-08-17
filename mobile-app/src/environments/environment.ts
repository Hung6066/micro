import {
  resolveMobileApiOrigin,
  resolveMobileRedirectUri,
  resolveMobileSentryDsn,
  resolveMobileSentryEnvironment,
  resolveMobilePushNotificationsEnabled,
} from '../app/core/mobile-runtime';

const apiOrigin = resolveMobileApiOrigin();

export const environment = {
  production: false,
  adminApiUrl: `${apiOrigin}/api/v1/admin`,
  oidc: {
    authority: apiOrigin,
    clientId: 'his-hope-mobile',
redirectUrl: resolveMobileRedirectUri('/auth/callback'),
postLogoutRedirectUri: resolveMobileRedirectUri('/auth/logout-callback'),
    scope: 'openid profile email roles hishop:permissions offline_access',
    secureRoutes: ['/api/v1/'],
  },
  appVersion: '0.1.0',
  // Configure the GlitchTip project DSN through the release environment.
  sentryDsn: resolveMobileSentryDsn(''),
  sentryEnvironment: resolveMobileSentryEnvironment('development'),
  // Native debug builds now ship google-services.json / GoogleService-Info.plist.
  // Web preview remains safe because NativeCapabilityService gates registration
  // with Capacitor.isNativePlatform(). Ops can still disable push at runtime.
  pushNotificationsEnabled: resolveMobilePushNotificationsEnabled(true),
  security: {
    // Release builds must replace these with the production SPKI hashes.
    certificatePins: [{ host: 'api.his-hope.example', sha256Spki: 'sha256/REPLACE_IN_RELEASE' }],
  },
};
