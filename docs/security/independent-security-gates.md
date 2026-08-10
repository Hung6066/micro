# Independent security gates

The repository does not self-certify OIDC conformance or penetration testing.
Release promotion requires two externally produced, signed evidence files:

- `artifacts/security/oidc-conformance/report.json`
- `artifacts/security/penetration-test/report.json`

Each report must contain `status: "passed"`, `assessor`, `reportUri`, and
`completedAt`. Run `scripts/verify-independent-security-evidence.ps1` in the
release pipeline. Missing evidence is a release failure, not an unverified
success.
