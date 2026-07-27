# His.Hope OIDC external partner integration

This document describes how an external partner application integrates with His.Hope Identity Service.

## 1. Security model

- Authorization Code + PKCE (`S256`) is required for interactive applications.
- Public clients use `token_endpoint_auth_method=none` and never receive a secret.
- Confidential clients use `client_secret_basic`; the secret is returned once at registration or rotation.
- Certificate-based clients use `private_key_jwt`: submit the partner public JWKS during registration and keep the private key outside His.Hope. Never upload or store a private key in Identity Service.
- Redirect URIs are exact matches. Wildcards are not permitted.
- Request only the minimum scopes required. Patient and clinical data scopes require separate API permissions.

## 2. Discovery and endpoints

Use the issuer configured for the environment:

```text
GET {issuer}/.well-known/openid-configuration
```

The response is authoritative for:

- `authorization_endpoint`
- `token_endpoint`
- `jwks_uri`
- supported scopes and grant types

Do not hard-code development URLs in partner applications.

## 3. Admin registration

An administrator can create a client at:

```http
POST /api/v1/admin/clients
Authorization: Bearer <admin-access-token>
Content-Type: application/json
```

Example public client:

```json
{
  "clientId": "partner-clinic-portal",
  "displayName": "Partner Clinic Portal",
  "type": "public",
  "grantTypes": ["authorization_code", "refresh_token"],
  "redirectUris": ["https://portal.partner.example.com/auth/callback"],
  "postLogoutRedirectUris": ["https://portal.partner.example.com/logout/callback"],
  "scopes": ["openid", "profile", "email"],
  "facilityId": "facility-001"
}
```

Example confidential client:

```json
{
  "clientId": "partner-backend",
  "displayName": "Partner Backend",
  "type": "confidential",
  "grantTypes": ["client_credentials"],
  "redirectUris": [],
  "scopes": ["hishop:appointments"],
  "facilityId": "facility-001"
}
```

The response contains `clientSecret` only for a confidential client. Store it in the partner secret manager immediately.

## 4. Dynamic onboarding

Dynamic registration is disabled unless `OpenIddict:DynamicRegistrationToken` is configured as a secret in the environment. With that bootstrap token:

```http
POST /connect/register
X-Registration-Token: <bootstrap-token>
Content-Type: application/json
```

```json
{
  "clientName": "Partner Portal",
  "redirectUris": ["https://portal.partner.example.com/auth/callback"],
  "postLogoutRedirectUris": ["https://portal.partner.example.com/logout/callback"],
  "grantTypes": ["authorization_code", "refresh_token"],
  "scopes": ["openid", "profile", "email"],
  "tokenEndpointAuthMethod": "none"
}
```

The endpoint issues a generated `client_id`. The bootstrap token must be delivered through a separate partner onboarding channel, rotated regularly, rate-limited at the edge, and never exposed to browser code.

## 5. Authorization code + PKCE

```text
GET {issuer}/connect/authorize?
  client_id=partner-clinic-portal&
  redirect_uri=https%3A%2F%2Fportal.partner.example.com%2Fauth%2Fcallback&
  response_type=code&
  scope=openid%20profile%20email&
  code_challenge=<base64url-sha256-verifier>&
  code_challenge_method=S256&
  state=<random-state>&
  nonce=<random-nonce>
```

The user reviews the consent screen. On approval, exchange the code at `/connect/token` with the original `code_verifier`.

## 6. Certificate / private_key_jwt onboarding

The partner generates an RSA or EC key pair and submits only its public JWKS. The private key stays in the partner HSM/KMS. Registration uses `token_endpoint_auth_method=private_key_jwt` and the `jwks` field. Rotate keys by registering a new `kid`, validating both keys during the overlap window, then removing the old key.

Certificate mTLS (`tls_client_auth`) requires a trusted CA and ingress client-certificate enforcement. It must be enabled per environment at the gateway; do not treat a PEM upload alone as mTLS authentication.

## 7. Consent and revocation

Consent is stored per user, client and scope set. Adding a new scope shows consent again. Users can revoke a client from the consent management page/API. Partners must handle:

- `access_denied`
- `invalid_grant`
- `invalid_client`
- `insufficient_scope`
- token expiry and refresh-token rotation failures

## 8. Production checklist

- Use HTTPS everywhere except local development.
- Register exact redirect and post-logout URLs.
- Use PKCE for every authorization-code client.
- Store client secrets/private keys in a managed secret store or HSM.
- Validate issuer, audience, signature, expiry and scopes in every API.
- Never log authorization codes, tokens, secrets or patient data.
- Monitor registration, rotation, consent, revoke and failed-token events through the audit stream.
