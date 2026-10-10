# His.Hope 3P Security Status

> Status is evidence-based. A checklist item is only marked complete when the
> repository contains an enforceable control or a repeatable CI gate.

## P0: Production blockers

| Control | Status | Evidence | Remaining action |
|---|---|---|---|
| JWT issuer and audience validation | Implemented | All service JWT handlers validate `Jwt:Issuer` and `Jwt:Audience` | Set both values in every production deployment |
| HTTPS metadata outside Development | Implemented | Service handlers and SystemDashboard BFF use environment-aware metadata validation | Use HTTPS Identity discovery URL in production |
| Explicit CORS origins | Implemented | API Gateway and SystemDashboard BFF fail startup without configured origins | Keep origin lists environment-specific |
| Hard-coded BFF signing secret | Removed | SystemDashboard BFF reads authority/issuer/audience only | Rotate any previously exposed value |
| NU1605 / NU1903 dependency warnings | Enforced | Restore/build gates treat both IDs as errors | Keep NuGet audit feed available in CI |
| Production frontend dependency audit | Enforced | `security-quality-gate.yml` audits all three apps with `--omit=dev` | Remediate dev-tool vulnerabilities on their release cadence |
| Container runtime hardening | Repository/CI enforced; runtime pending | Non-root Docker images, restricted K8s contexts, no Docker socket, immutable production digest component, HA/data-plane validators and fail-closed release contracts | Verify the selected Harbor images/signatures and admission provider on the production cluster |
| Docker socket exposure | Removed | SystemDashboard BFF no longer mounts `/var/run/docker.sock`; local lifecycle control is disabled | Use only the scoped Kubernetes Role in production |

## P1: Enterprise assurance

| Control | Status | Evidence | Remaining action |
|---|---|---|---|
| API authorization | Implemented and tested | Permission policies on Identity admin endpoints; service authorization enabled; negative authorization and tenant-scope tests are part of the verification matrix | Keep resource-level negative coverage required for every new endpoint |
| Correlation and ProblemDetails | Implemented and contract-checked | Shared middleware, `His.Hope.Core` API problem contract and communication-boundary validation | Retain authenticated gateway/BFF runtime smoke evidence for each release |
| Rate limiting and revocation | Implemented; local degradation coverage added, protected resilience proof pending | Shared rate limiting, Redis token blacklist and refresh reuse handling; fallback counters remain isolated per client during Redis failure and are regression-tested | Complete protected load/failure testing for distributed limits, Redis outage and recovery |
| Security headers | Implemented | Shared `UseSecurityHeaders` middleware | Maintain CSP per frontend deployment needs |
| SBOM and vulnerability evidence | Implemented | Trivy SARIF and CycloneDX artifact in CI | Store artifacts under release retention policy |
| Repository secret hygiene | CI enforced; history purge pending if previously exposed | Captured cookie/auth artifact deny-list, protected-secret-only synthetic login, and fail-closed tracked-file scan | Revoke/rotate affected credentials and verify approved Git-history and remote artifact purge |
| Accessibility/keyboard quality gate | CI enforced; authenticated runtime pending | Shared foundation axe/interaction coverage and required CI Playwright accessibility/keyboard/visual job | Supply protected URLs/credentials and retain a complete authenticated Chromium result as release evidence |
| Threat model and abuse cases | Repository/CI enforced; residual production risk pending | Structured five-flow trust-boundary catalog, six STRIDE abuse cases with owners/mitigations/evidence, and fail-closed validator | Obtain Security/Clinical Safety sign-off and close the explicit residual production risk cases |

## P2: Governance and continuous compliance

| Control | Status | Evidence | Remaining action |
|---|---|---|---|
| SLSA provenance | CI enforced; registry verification pending | Release workflow generates SLSA provenance and GitOps promotion verifies the attestation before digest promotion | Verify the published provenance against the real registry and retain immutable release evidence |
| Image signing enforcement | Deny policy configured; cluster proof pending | Production overlay applies Gatekeeper Cosign signature constraint with `deny`; source and promotion contracts validate policy alignment | Deploy and health-check the Ratify/cosign ExternalData provider, then run positive/negative admission probes |
| HIPAA audit controls | Repository/CI enforced; production WORM/DR pending | Durable database audit, redaction, append-only contracts, access-review expiry/revocation, replica-safe processing, SIEM/WORM failure envelope in Redis DLQ, and tamper drill are covered | Prove locked WORM retention, off-cluster restore and quarterly signed operational evidence in production |
| CWE/API Security/ASVS mapping | Endpoint-level baseline enforced; full requirement mapping pending | ASVS/CWE catalog validator enriches the strict 212-endpoint inventory with control IDs and writes `artifacts/evidence/security-assurance-endpoint-mapping.json`; no endpoint classification is unmapped | Complete the requirement-to-test-to-evidence mapping for every externally exposed endpoint |
| Dependency SLA | Repository/CI enforced; operational clock pending | `config/dependency-risk-policy.v1.json` defines critical/high/moderate/low remediation windows; validator rejects expired or incomplete exceptions; CI runs it alongside SCA/SBOM/SARIF gates | Triage each live finding within the policy window and retain approved exception evidence when remediation cannot meet the SLA |
| Access review | Runtime automation implemented; operational evidence pending | RBAC, scoped assignments, audit endpoints, and `AccessReviewExpiryWorker` automatically revoke overdue review subjects; Redis sweep lock prevents duplicate processing across replicas; Identity integration suite covers expiry/revocation and lock contention | Run quarterly user/role/client recertification with signed evidence and retain production worker execution/audit proof |

## Release gate

The production release is **not** 10/10 solely because a build passes. It is
ready only when P0 controls pass, P1 required checks are green, and the P2
exceptions above have an owner, expiry date, and compensating control.
