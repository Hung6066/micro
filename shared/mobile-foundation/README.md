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
