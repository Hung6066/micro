# Independent assessment handoff

This document defines the external evidence required before production promotion.
The repository must not generate these reports itself.

## OIDC conformance

The assessor runs the official OpenID Connect conformance suite against the
production issuer and records the suite version, test profile, issuer, client
registration, and result URL. At minimum the profile must cover authorization
code + PKCE, refresh-token rotation, discovery/JWKS, logout, nonce/state,
redirect URI validation, and the mobile DPoP token-binding profile where the
selected suite supports it.

## Penetration test

The assessor tests the production gateway, Identity Service, browser clients,
mobile API surface, native callback/deep-link handling, DPoP replay/binding,
JWE confidentiality, passkeys, SAML/LDAP federation, push-token registration,
rate limits, authorization boundaries, and PHI leakage in logs/errors.

## Evidence contract

Place the assessor-produced files at:

- `artifacts/security/oidc-conformance/report.json`
- `artifacts/security/penetration-test/report.json`

Each JSON document must contain:

```json
{
  "assessmentType": "oidc-conformance",
  "evidenceSource": "external-independent",
  "status": "passed",
  "assessor": "independent assessor organization",
  "reportUri": "https://assessor.example/report/123",
  "completedAt": "2026-07-29T00:00:00Z",
  "signature": {
    "algorithm": "cosign",
    "verified": true,
    "verificationUri": "https://assessor.example/evidence/123.sig"
  }
}
```

The penetration-test file uses `"assessmentType": "penetration-test"`.
The `evidenceSource` marker and verified HTTPS signature metadata are mandatory
so that locally generated automated reports cannot be promoted as independent
assurance.
Run `scripts/verify-independent-security-evidence.ps1` only after the
assessor has supplied the real reports. Missing, malformed, or non-HTTPS
evidence is a release failure.
