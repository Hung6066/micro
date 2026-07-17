# BAA Onboarding Checklist

> **Document Version:** 1.0  
> **Last Updated:** 2026-07-17  
> **Audience:** Security team, compliance officers, procurement  
> **Status:** Template — use per-vendor when onboarding a new business associate

---

## Purpose

This checklist defines the four-phase process for onboarding a new Business Associate (vendor) that may touch electronic Protected Health Information (ePHI). It satisfies HIPAA 45 CFR 164.308(b)(1) — Business Associate Contracts.

**Use this checklist for every new vendor identified in the [BAA Vendor Registry](./BAA-VENDOR-REGISTRY.md).**

---

## Phase 1: Assessment — Does the Vendor Touch ePHI?

Before any engagement, determine whether the vendor requires a BAA.

| # | Check | Done | Notes |
|---|-------|------|-------|
| 1.1 | Does the vendor **store** any ePHI (database, cache, backup, log)? | ☐ | |
| 1.2 | Does the vendor **process/transform** ePHI (compute, analytics, ML)? | ☐ | |
| 1.3 | Does the vendor **transmit** ePHI (message broker, API gateway, CDN)? | ☐ | |
| 1.4 | Does the vendor have **administrative access** to systems containing ePHI (support, DB admin, K8s admin)? | ☐ | |
| 1.5 | Does the vendor **index or search** ePHI (log aggregation, search engine, SIEM)? | ☐ | |
| 1.6 | Does the vendor **cache or transiently store** ePHI (Redis, CDN, proxy)? | ☐ | |
| 1.7 | **Assessment Result:** Vendor touches ePHI → proceed to Phase 2. | ☐ | |
| 1.8 | **Assessment Result:** Vendor does NOT touch ePHI → document exemption and file. | ☐ | Document why no BAA is needed |

**If any of 1.1–1.6 is YES, the vendor needs a BAA. Proceed to Phase 2.**

---

## Phase 2: BAA Execution — Legal Review and Signing

Obtain, review, and execute the Business Associate Agreement.

| # | Check | Done | Assigned To | Notes |
|---|-------|------|-------------|-------|
| 2.1 | Request BAA from vendor (use vendor's standard HIPAA BAA if available) | ☐ | | |
| 2.2 | Review BAA for compliance with HIPAA Security Rule (164.314(a)) | ☐ | Legal + Security | |
| 2.3 | Verify BAA includes required provisions: | ☐ | Legal | |
| | — Permitted uses and disclosures | ☐ | | |
| | — Prohibition on further disclosures (unless authorized) | ☐ | | |
| | — Safeguards (appropriate administrative, physical, technical) | ☐ | | |
| | — Breach notification obligations (45 CFR 164.410) | ☐ | | |
| | — Return or destruction of ePHI upon termination | ☐ | | |
| | — Agent/subcontractor BAA requirements | ☐ | | |
| | — Audit/inspection rights | ☐ | | |
| | — Indemnification for BAA breach | ☐ | | |
| 2.4 | Negotiate any gaps or missing provisions with vendor | ☐ | Legal | |
| 2.5 | Obtain final approved BAA signed by both parties | ☐ | Legal | |
| 2.6 | Store signed BAA in secure document repository | ☐ | Security | Location: `docs/security/baa/` |
| 2.7 | Record BAA status in [BAA Vendor Registry](./BAA-VENDOR-REGISTRY.md) | ☐ | Security | Update status to SIGNED |
| 2.8 | Notify engineering team that BAA is in place | ☐ | Security | |

---

## Phase 3: Technical Controls — Secure the Integration

After BAA execution, implement required technical safeguards.

| # | Check | Done | Notes |
|---|-------|------|-------|
| 3.1 | **Encryption in Transit:** Verify TLS 1.2+ / mTLS is configured for all connections to this vendor | ☐ | |
| 3.2 | **Encryption at Rest:** Verify vendor encrypts data at rest (AES-256 or equivalent) | ☐ | |
| 3.3 | **Access Control:** Restrict vendor access to minimum necessary ePHI (least privilege) | ☐ | |
| 3.4 | **Network Segmentation:** Vendor services isolated via Cilium network policies (default-deny) | ☐ | |
| 3.5 | **Authentication:** Vendor access uses Vault-managed credentials or mTLS (no shared secrets) | ☐ | |
| 3.6 | **Audit Logging:** Enable audit logging for all vendor data access | ☐ | |
| 3.7 | **Incident Response:** Vendor added to incident notification list (PagerDuty / Slack) | ☐ | |
| 3.8 | **Data Classification:** Tag vendor-linked resources with `security.his.hope/data-classification: phi` | ☐ | |
| 3.9 | **Backup/DR:** Verify vendor data is included in backup and disaster recovery plans | ☐ | |
| 3.10 | **Breach Notification:** Confirm vendor's breach notification process and contact list | ☐ | |

---

## Phase 4: Ongoing — Annual Review and Monitoring

Continuous compliance after onboarding.

| # | Check | Frequency | Done | Notes |
|---|-------|-----------|------|-------|
| 4.1 | Verify vendor has active, signed BAA on file | Annual | ☐ | |
| 4.2 | Review vendor's SOC 2 Type II or HIPAA attestation report | Annual | ☐ | |
| 4.3 | Audit vendor access logs for anomalous PHI access | Quarterly | ☐ | Via ELK alerts |
| 4.4 | Verify encryption standards still meet policy (no downgraded ciphers) | Annual | ☐ | |
| 4.5 | Review vendor's data retention / deletion compliance | Annual | ☐ | |
| 4.6 | Check for vendor security incidents or breaches reported in the last year | Annual | ☐ | |
| 4.7 | Confirm vendor's subcontractors are also BA-compliant (flow-down BAA) | Annual | ☐ | |
| 4.8 | Update [BAA Vendor Registry](./BAA-VENDOR-REGISTRY.md) expiry/review date | Annual | ☐ | |
| 4.9 | Notify vendor of any changes to His.Hope security requirements | As needed | ☐ | |

---

## Vendor Mapping

Use this section to track per-vendor progress through the four phases.

| Vendor | Phase 1 (Assessment) | Phase 2 (BAA Exec) | Phase 3 (Technical) | Phase 4 (Ongoing) | Status |
|--------|---------------------|-------------------|--------------------|--------------------|--------|
| CockroachDB Labs | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |
| Google Cloud | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |
| Elastic | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |
| Redis Labs | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |
| VMware (RabbitMQ) | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |
| PagerDuty | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |
| Slack | ☐ | ☐ | ☐ | ☐ | ❌ Not Started |

---

## References

- **HIPAA 45 CFR 164.308(b)(1)** — Business Associate Contracts
- **HIPAA 45 CFR 164.314(a)** — Organizational requirements for BA contracts
- **HIPAA 45 CFR 164.410** — Breach notification by business associate
- **NIST SP 800-66 Rev. 2** — Implementing the HIPAA Security Rule
- **His.Hope BAA Vendor Registry** — [`BAA-VENDOR-REGISTRY.md`](./BAA-VENDOR-REGISTRY.md)
- **His.Hope HIPAA Compliance** — [`hipaa-compliance.md`](./hipaa-compliance.md)
- **His.Hope Security Incident Response** — [`../operations/security-incident-response.md`](../operations/security-incident-response.md)

---

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-07-17 | Initial checklist created | Security Team |
