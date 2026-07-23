# ADR 013: OpenIddict OAuth2/OIDC Identity Service

**Date:** 2026-07-23
**Status:** Accepted
**Supersedes:** ADR 012 (RSA asymmetric JWT via Vault)

## Context

The Identity Service used custom HMAC-SHA256 JWT tokens with a hardcoded key fallback. This prevented OAuth2/OIDC interoperability, posed security risks (any service with the shared key could forge tokens), and lacked standards compliance.

## Decision

Adopt OpenIddict 5.x as the OAuth2/OIDC authorization server, hosted inside the existing IdentityService.Api process. Key decisions:

1. **OpenIddict over Duende IdentityServer**: Open-source, .NET-native, OpenID Connect certified, no licensing cost.
2. **RS256 via Vault transit**: Private signing keys live in Vault, never exposed. Development uses ephemeral RSA-2048.
3. **gRPC introspection**: Other services validate tokens via gRPC call to IdentityService (not local JWT verify). Enables instant revocation.
4. **Authorization Code + PKCE**: Primary flow for SPA/BFF. PKCE mandatory for all public clients.
5. **Dual-mode migration**: Legacy /api/v1/auth/* endpoints coexist with OIDC /connect/* for 2 releases.

## Consequences

- **Positive**: Standard OAuth2/OIDC compliance, interoperable tokens, no hardcoded secrets, instant token revocation.
- **Negative**: Vault dependency (startup fails if Vault unreachable), gRPC introspection adds ~2ms latency per auth check.
- **Neutral**: Maintains existing ASP.NET Core Identity user store. No schema changes to user/role/permission tables.
