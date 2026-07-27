# Hybrid Mobile Foundation Design

## Runtime

The first implementation uses Angular in a Capacitor WebView. Capacitor supplies the native bridge; it is not a second UI framework. Ionic UI components may be introduced later only when a mobile-specific interaction needs them.

## Authentication

The mobile app is a public OIDC client:

```text
mobile app -> system browser -> Identity Service -> hishope://auth/callback
           -> PKCE verifier -> token exchange -> secure native storage
```

Production must use platform-backed storage and verified app/universal links. The development redirect is intentionally configurable per emulator/device.

## Shared versus mobile-specific

| Concern | Shared | Mobile-specific |
|---|---|---|
| Design tokens and typography | `frontend-foundation` | none |
| i18n/theme/permissions | `frontend-foundation` | native lifecycle sync |
| REST contracts | `His.Hope.Contracts` / generated clients | retry/offline adapter |
| OIDC protocol | Identity Service contract | system-browser redirect |
| Secure token storage | interface | Keychain/Keystore adapter |
| Push/biometric/deep link | interface | Capacitor plugins |
| Patient workflows | domain API and shared UX | mobile navigation/presentation |

## Security requirements

- No embedded login WebView.
- Authorization Code + PKCE only.
- No access or refresh token in localStorage.
- Refresh-token rotation and reuse detection remain server-enforced.
- Redirect URI allow-list is exact and environment-specific.
- Certificate pinning is a deployment decision, not a UI concern.
- Device logs must redact tokens and patient data.
