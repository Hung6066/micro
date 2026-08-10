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

## Production Deployment Guardrails

The production deployment is considered valid only when these controls are present together:

1. **Persistent RS256 signing in production**: OpenIddict signing must use a persistent RSA private key provided through `OpenIddict__Signing__PrivateKeyPath` or `Jwt__RsaPrivateKeyPath`. Production must not rely on ephemeral keys, `Jwt__Key`, or any HMAC fallback. Vault transit remains the target key-management backend; until a Vault-backed crypto provider is wired into OpenIddict, the private key path must be delivered by the production secret mechanism.
2. **Fail-fast startup**: Identity pods must block startup when persistent signing material is unavailable. If Vault is the secret delivery backend, Kubernetes deployment should include a Vault wait/init path plus `/health/startup` probing.
3. **Stable issuer and endpoints**: `OpenIddict__Issuer` must match the public issuer used by BFFs and services. Discovery, JWKS, token, logout, and introspection endpoints are part of the release contract.
4. **Secret source of truth**: Kubernetes secrets for Vault AppRole credentials and database credentials are the operational boundary. Do not bake signing material into container images, ConfigMaps, or Compose defaults.
5. **Rotation readiness**: Vault transit key rotation is allowed without redeploying services. Operators must verify new `kid` values through JWKS/discovery and token issuance smoke tests after rotation.

The current manifest touchpoints are:

- `k8s/base/identity-service.yaml`: Identity deployment, service ports, signing-key/Vault env wiring, and startup probe.
- `k8s/vault/vault-csi-provider.yaml`: SecretProviderClass entries for Identity Service Vault-backed secrets.
- `k8s/vault/vault-agent-injector.yaml`: cluster sidecar injector configuration; webhook failure policy must be reviewed before production hardening sign-off.
- `docker/docker-compose.yml`: local development stack; any ephemeral or legacy `Jwt__Key` fallback here is not a production authorization path.
