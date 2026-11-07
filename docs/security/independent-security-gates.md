# Independent security gates

The repository does not self-certify OIDC conformance or penetration testing.
Release promotion requires two externally produced, signed evidence files:

- `artifacts/security/oidc-conformance/report.json`
- `artifacts/security/penetration-test/report.json`

Each report must contain `evidenceSource: "external-independent"`,
`status: "passed"`, `assessor`, `reportUri`, `completedAt`, and verified
signature metadata:

```json
"signature": {
  "algorithm": "cosign",
  "verified": true,
  "verificationUri": "https://assessor.example/evidence/123.sig"
}
```

Automated repository reports must use a different source marker and can never
satisfy this gate. Run `scripts/verify-independent-security-evidence.ps1` in
the release pipeline. Missing, automated, unsigned, or malformed evidence is
environment-blocked/fail-closed, never an unverified success.
