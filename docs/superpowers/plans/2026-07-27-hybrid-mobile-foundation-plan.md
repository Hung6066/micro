# His.Hope Hybrid Mobile Foundation Plan

## Goal

Ship one Angular + Capacitor mobile workspace for Android and iOS while keeping the existing web applications on the same design, identity, permission and API contracts.

## Boundaries

- `@his-hope/frontend-foundation` remains the Angular/web UI package.
- `@his-hope/mobile-foundation` owns mobile capability contracts and platform-neutral helpers.
- `mobile-app` owns app composition, OIDC configuration and native adapter registration.
- Native plugins are adapters; feature pages do not call Capacitor directly.

## Phases

1. **Foundation and shell**
   - Add mobile package and Angular workspace.
   - Reuse shared brand, theme, i18n, offline and toast primitives.
   - Add a buildable authentication shell.
2. **Identity**
   - Register `his-hope-mobile` as a public OIDC client.
   - Use Authorization Code + PKCE with system-browser redirects.
   - Add Android App Links and iOS Universal Links before production release.
3. **Native capabilities**
   - Implement secure storage with Keychain/Keystore.
   - Add biometric unlock, push registration, network lifecycle and deep links.
4. **Clinical vertical slices**
   - Patient search, patient workspace, appointments and alerts.
   - Reuse REST contracts and SignalR/SSE only through mobile adapters.
5. **Release gates**
   - Android/iOS signed builds.
   - OIDC integration tests, offline/reconnect tests, accessibility and performance checks.

## Done criteria for the first vertical slice

- `npm --workspace @his-hope/mobile-app run build` passes.
- `npm --workspace @his-hope/mobile-foundation run build` passes.
- OIDC client uses code flow and PKCE; no access token is placed in localStorage by app code.
- UI imports shared foundation components and tokens.
- Native capability calls are behind `@his-hope/mobile-foundation` contracts.
