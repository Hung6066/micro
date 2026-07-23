# Identity Service Security Audit

**Date:** 2026-07-23
**Scope:** Phases 1-4 (OIDC, Client Management, Federation, Provisioning)
**Standard:** OWASP Top 10 (2021), OAuth 2.0 Security BCP, HIPAA 164.312

## 1. Authentication Security

| Check | Status | Notes |
|-------|--------|-------|
| PKCE enforced for all public clients | ✅ | `RequireProofKeyForCodeExchange()` |
| Authorization codes single-use | ✅ | 60s TTL via Redis, GET+DEL atomic |
| Refresh token rotation | ✅ | OpenIddict built-in rotation with reuse detection |
| Token binding (IP + user) | ✅ | Redis binding via TokenBindingService |
| Account lockout | ✅ | 5 attempts → 15min lockout |
| MFA support (TOTP) | ✅ | RFC 6238 implementation |
| No hardcoded secrets | ✅ | Removed in Phase 1, Vault-only |
| Constant-time secret comparison | ✅ | `CryptographicOperations.FixedTimeEquals` |

## 2. Authorization Security

| Check | Status | Notes |
|-------|--------|-------|
| RBAC with 49 permissions | ✅ | PermissionHandler + gRPC check |
| Least privilege seeds | ✅ | 7 roles with scoped permissions |
| Admin endpoints require auth | ✅ | `.RequireAuthorization()` |
| Circuit breaker (fail-closed) | ✅ | Polly: 3 failures → open → deny |
| Token introspection validates binding | ✅ | IP + user + jti check |

## 3. OAuth2/OIDC Attack Surface

| Attack Vector | Mitigation | Status |
|---------------|-----------|--------|
| CSRF on authorize | state parameter + PKCE | ✅ |
| Code injection | PKCE verifier validation | ✅ |
| Token replay | jti blacklist + IP binding | ✅ |
| Client impersonation | Client secret validation | ✅ |
| Redirect URI manipulation | Strict URI matching in OpenIddict | ✅ |
| Scope escalation | Server-enforced scope validation | ✅ |
| Mix-up attacks | Issuer validation in OpenIddict | ✅ |

## 4. API Security

| Check | Status |
|-------|--------|
| CORS restricted origins | ✅ |
| Security headers (HSTS, XFO, CSP) | ✅ |
| Rate limiting on auth endpoints | ✅ |
| Input validation (FluentValidation) | ✅ |
| SQL injection protection (EF Core parameterized) | ✅ |
| Correlation IDs for tracing | ✅ |

## 5. Data Protection

| Check | Status |
|-------|--------|
| Tokens in HttpOnly cookies (BFF) | ✅ |
| Refresh tokens never in browser JS | ✅ |
| Client secrets in Vault (not DB) | ✅ |
| Audit logging for all token ops | ✅ |
| PHI audit trail (HIPAA 164.312(b)) | ✅ |
| Personal data minimization | ✅ |

## 6. Findings & Recommendations

### High Priority
- **Key rotation automation**: Manual only via admin API. Automate via Vault auto-rotate.
- **DPoP**: Not yet implemented. Add for Phase 5+.

### Medium Priority
- **SAML**: Not implemented. Add for legacy hospital IdP integration.
- **FIDO2/WebAuthn**: Not implemented. Add for phishing-resistant MFA.

### Low Priority
- **Token encryption (JWE)**: Access tokens are signed only, not encrypted. Full JWE support for sensitive claims.
