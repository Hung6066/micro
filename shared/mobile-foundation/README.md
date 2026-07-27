# His.Hope Mobile Foundation

This package defines the native capability boundary for Angular + Capacitor applications.

It deliberately contains contracts and deterministic helpers only. Native implementations must be provided by Capacitor plugins or platform adapters in the mobile app.

Required production adapters:

- secure storage backed by Android Keystore and iOS Keychain;
- biometric authentication;
- push notification registration;
- app/universal-link handling;
- network and lifecycle state.

OIDC uses Authorization Code + PKCE in the system browser. Tokens must not be stored in web `localStorage` on native builds.

Native refresh uses the `HisHopeNativeRefreshCapability` adapter contract. The shared Angular refresher keeps a browser/touch fallback; an Ionic `ion-refresher` or dedicated Capacitor plugin can implement the native contract without changing feature pages.

## Angular adapter

`mobile-app/src/app/core/native-capability.service.ts` is the application adapter
for these contracts. Pages should depend on that service rather than importing
Capacitor plugins directly. In a browser it keeps the secure-storage and native
capabilities unavailable by default; on Android/iOS it delegates to Keystore,
Keychain, biometrics, Camera, Push Notifications, and App URL listeners.

Before production release, configure the platform credentials and server flow:

- register the device push token with an authenticated backend endpoint;
- configure FCM/APNs credentials, iOS push entitlements, and Android notification
  channels per environment;
- configure verified universal/app links and the registered OIDC redirect URI;
- keep the native secure-storage key namespace versioned and revoke it on logout
  or session revocation.
