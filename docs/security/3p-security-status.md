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
| Container runtime hardening | Partial with deny policy | Non-root Docker images, restricted K8s contexts, no Docker socket, and production digest policy | Populate release digest component with real registry digests and make the release check required |
| Docker socket exposure | Removed | SystemDashboard BFF no longer mounts `/var/run/docker.sock`; local lifecycle control is disabled | Use only the scoped Kubernetes Role in production |

## P1: Enterprise assurance

| Control | Status | Evidence | Remaining action |
|---|---|---|---|
| API authorization | Implemented | Permission policies on Identity admin endpoints; service authorization enabled | Add negative integration tests for every resource permission |
| Correlation and ProblemDetails | Implemented | Shared middleware and `His.Hope.Core` API problem contract | Validate propagation at gateway and BFF boundaries |
| Rate limiting and revocation | Implemented | Shared rate limiting, Redis token blacklist and refresh reuse handling | Load-test limits and Redis failure behavior |
| Security headers | Implemented | Shared `UseSecurityHeaders` middleware | Maintain CSP per frontend deployment needs |
| SBOM and vulnerability evidence | Implemented | Trivy SARIF and CycloneDX artifact in CI | Store artifacts under release retention policy |
| Accessibility/keyboard quality gate | Partial | Shared foundation has axe/interaction coverage | Make Playwright axe and keyboard jobs required branch checks |
| Threat model and abuse cases | Partial | OAuth/BFF reviews exist | Add reviewed data-flow diagrams and abuse-case owners |

## P2: Governance and continuous compliance

| Control | Status | Evidence | Remaining action |
|---|---|---|---|
| SLSA provenance | Partial | Cosign/Tekton tasks exist | Generate and verify provenance attestations for every release |
| Image signing enforcement | Deny policy configured | Production overlay applies Gatekeeper Cosign signature constraint with `deny` | Deploy and health-check the Ratify/cosign ExternalData provider before production admission |
| HIPAA audit controls | Partial | Audit events, correlation IDs and HIPAA documentation exist | Prove retention, immutability, access review and restore drills |
| CWE/API Security/ASVS mapping | Partial | OWASP checklist exists | Map requirements to tests and release evidence, not prose only |
| Dependency SLA | Open | CI reports production vulnerabilities | Define remediation windows and exception owner/expiry |
| Access review | Open | RBAC and audit endpoints exist | Schedule quarterly user/role/client review with signed evidence |

## Release gate

The production release is **not** 10/10 solely because a build passes. It is
ready only when P0 controls pass, P1 required checks are green, and the P2
exceptions above have an owner, expiry date, and compensating control.
