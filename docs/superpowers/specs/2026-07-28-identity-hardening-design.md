# Identity Hardening and Federation Design

## Goal

Close the remaining identity and mobile trust gaps without changing the web BFF
session model: sender-constrain mobile access tokens, add phishing-resistant
passkeys, support enterprise federation, encrypt token claims, enforce one
native pinned transport boundary on iOS, and make push-device state durable.

## Architecture

1. **Sender constraint**: mobile authorization-code and refresh-token requests
   carry a DPoP proof. The authorization server binds the access token to the
   proof key using `cnf.jkt`. APIs validate the DPoP proof, method/URI binding,
   clock window, unique `jti`, and the thumbprint before accepting the bearer
   token. Web BFF sessions remain HttpOnly and are not changed to DPoP.
2. **Passkeys**: WebAuthn credentials are stored as a separate durable table
   keyed by user and credential ID. Begin/complete registration and assertion
   endpoints use one-time server challenges, origin/RP-ID validation, user
   verification, signature-counter checks, revocation, and security audit
   events. Password/MFA recovery remains available with explicit step-up audit.
3. **Federation**: SAML is an external-login adapter with strict issuer,
   audience, signature, certificate rollover, replay, and clock validation.
   LDAP/AD uses LDAPS only, parameterized directory filters, bounded sync,
   JIT provisioning, group-to-role mapping, and no directory passwords in the
   application database.
4. **JWE**: remove the access-token encryption bypass. Signing and encryption
   keys remain separate and are loaded from the configured Vault/KMS material;
   production fails fast when encryption material is absent. Resource APIs
   receive the matching validation encryption key.
5. **Mobile transport**: discovery, authorization exchange, refresh, API,
   SignalR, and push-registration traffic use a single native iOS transport
   adapter when pinning is enabled. A pin mismatch is terminal for that
   request; no WebView fallback is allowed. Pins are environment-provided and
   rotated with overlap.
6. **Push/device state**: device registration stores a token hash for lookup,
   encrypted provider token, platform/provider metadata, timestamps, and
   revocation state. Registration is idempotent, unique per active user/device
   and provider, and delivered through an outbox/retry provider adapter for
   Firebase/APNs. Credentials are Vault/KMS-backed and never committed.

## Verification

- Unit/integration tests cover DPoP replay, wrong method/URI/key, token binding,
  passkey challenge replay and counter rollback, SAML signature/issuer failure,
  LDAP injection and LDAPS enforcement, and JWE decryptability/claim privacy.
- Native tests prove every iOS transport path goes through the pinned adapter
  and that pin failure cannot fall back to WebView networking.
- Provider tests use fake FCM/APNs servers; production gates require real Vault
  credentials and a durable database migration.
- OIDC conformance is run by the official independent conformance suite and
  stored as an artifact. Penetration testing remains an external execution
  gate; the repository supplies scope, seed data, and evidence upload checks.

## Non-goals

The repository cannot self-issue an independent OIDC certification or claim an
external penetration test. It can make both activities repeatable and fail the
release gate until signed external reports are attached.
