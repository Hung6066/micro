# His.Hope Mobile Foundation

This package defines the native capability boundary for Angular + Capacitor applications.

It deliberately contains contracts and deterministic helpers only. Native implementations must be provided by Capacitor plugins or platform adapters in the mobile app.

Required production adapters:

- secure storage backed by Android Keystore and iOS Keychain;
- biometric authentication;
- push notification registration;
- app/universal-link handling;
- network and lifecycle state.

## OIDC and native security contracts

The package owns reusable contracts and deterministic, platform-neutral
helpers; it does not own application routes or identity-provider policy.

- `HisHopeNativePasskeyCapability` is the seam for native FIDO2/passkey
  registration and assertion. Android is implemented by the app's
  `HisHopeSecurityPlugin` through AndroidX Credential Manager; iOS is provided
  by the same plugin boundary through AuthenticationServices. The Angular
  adapter passes server-generated WebAuthn JSON and returns the native response
  to the Identity Service.
- Mobile MFA enrollment uses the same adapter and the Identity Service
  `/api/v1/auth/passkeys/register/options` plus `/register/complete` endpoints.
  Passkey MFA is preferred; TOTP remains the explicit fallback for unsupported
  devices or cancelled native prompts.
- Native OIDC MFA approval uses an opaque one-time `hishope://auth/mfa` ticket.
  The browser keeps the pending OIDC cookie and polls the server; the native
  app only receives the ticket, signs the server challenge, and never receives
  or transports an OIDC cookie or access token through the deep link.
- `HisHopeDpopProofService` and `HisHopeWebCryptoDpopProofService` create an
  ES256 DPoP proof with `htu`, `htm`, `iat`, `jti` and `ath` when an access token
  is present. The P-256 private JWK is persisted through the supplied secure
  storage adapter; a missing key must fail closed.
- `HisHopeNativeSecurityCapability` describes device-security and certificate
  pinning boundaries. Pin values, issuer URLs and redirect URIs remain
  deployment configuration.

The mobile OIDC flow is Authorization Code + PKCE through the system browser
or native authorization surface, followed by an allow-listed deep-link
callback. Tokens belong in Keychain/Keystore-backed storage, never web
`localStorage`. The app adapter is
`mobile-app/src/app/core/mobile-platform.service.ts`; feature pages should use
`NativeCapabilityService` instead of importing Capacitor or native plugin APIs.

The shared package cannot certify a native build by itself. Android Gradle
compilation and unit tests are repository gates; emulator/device login,
passkey prompts, callback delivery and iOS compilation remain platform runtime
gates.

OIDC uses Authorization Code + PKCE in the system browser. Tokens must not be stored in web `localStorage` on native builds.

Native refresh uses the `HisHopeNativeRefreshCapability` adapter contract. The shared Angular refresher keeps a browser/touch fallback; an Ionic `ion-refresher` or dedicated Capacitor plugin can implement the native contract without changing feature pages.

## Angular shell (`@his-hope/mobile-foundation/angular`)

Import the Angular entry point when bootstrapping a new Capacitor app:

```typescript
import {
  provideHisHopeMobilePlatformAdapters,
  HIS_HOPE_SECURE_STORAGE,
  HIS_HOPE_BIOMETRIC,
  HIS_HOPE_APP_PIN,
  HIS_HOPE_OFFLINE_SYNC_CONFIG,
  HIS_HOPE_MOBILE_AUTH,
  HIS_HOPE_TABLE_API_BASE_URL,
  hisHopeMobileSessionInterceptor,
  createHisHopePermissionReadGuard,
  HisHopeMobileLockService,
  HisHopeMobilePagedResourceController,
  HisHopeResourceTableController,
  HisHopeResourceStateController,
  HisHopeMobileTableApiService,
} from '@his-hope/mobile-foundation/angular';
```

The host app supplies platform adapters through DI tokens (`HIS_HOPE_SECURE_STORAGE`, `HIS_HOPE_BIOMETRIC`, `HIS_HOPE_APP_PIN`, `HIS_HOPE_MOBILE_AUTH`, `HIS_HOPE_OFFLINE_SYNC_CONFIG`, `HIS_HOPE_TABLE_API_BASE_URL`). Feature routes, API services, and permission maps remain in the host app.

See `mobile-app/src/app/core/mobile-foundation.providers.ts` for the reference Identity Admin wiring.

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
