# OWASP Top 10 (2021) Compliance Checklist

## A01: Broken Access Control
- [x] Permission-based authorization with 49 granular permissions
- [x] Admin endpoints require explicit authorization
- [x] Circuit breaker fail-closed

## A02: Cryptographic Failures
- [x] RS256 signing (no hardcoded keys)
- [x] Vault transit for production key storage
- [x] HTTPS enforced (K8s mTLS + Linkerd)
- [x] Constant-time secret comparison

## A03: Injection
- [x] EF Core parameterized queries (SQL injection)
- [x] FluentValidation input validation
- [x] No LDAP injection (Novell library handles encoding)

## A04: Insecure Design
- [x] Threat modeling: OAuth2 attack vectors documented
- [x] Security audit completed

## A05: Security Misconfiguration
- [x] Security headers (HSTS, XFO, CSP)
- [x] CORS restricted origins
- [x] Production config separate from dev

## A06: Vulnerable Components
- [x] NuGet packages pinned (no floating versions)
- [x] OWASP Dependency Check in CI (recommended)

## A07: Identification & Auth Failures
- [x] Account lockout (5 attempts)
- [x] MFA (TOTP)
- [x] Password policy (8+ chars, digit, special)
- [x] External IdP federation with account linking

## A08: Software & Data Integrity
- [x] NuGet package integrity (lock files)
- [x] Container image signing (Cosign recommended)

## A09: Security Logging & Monitoring
- [x] Audit logs for all token operations
- [x] Security event logging (login attempts, threats)
- [x] Correlation IDs for tracing

## A10: SSRF
- [x] No user-controlled URLs in server requests
- [x] External IdP redirect URIs validated
