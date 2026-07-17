# His.Hope BAA Vendor Registry

> **Document Version:** 1.0  
> **Last Updated:** 2026-07-17  
> **Audience:** Security team, compliance officers, procurement  
> **Status:** Active — tracking BAA execution for all ePHI-touching vendors

---

## Purpose

This registry documents all third-party vendors that process, store, or transmit electronic Protected Health Information (ePHI) on behalf of His.Hope. Under HIPAA (45 CFR 164.308(b)(1) — Business Associate Contracts), a Business Associate Agreement (BAA) is required **before** any vendor with ePHI access is onboarded.

Each entry includes:
- Vendor name and service
- ePHI exposure level (how the vendor touches PHI)
- BAA execution status
- Priority for signing
- Responsible team and notes

---

## Vendor Registry

### 1. CockroachDB Labs

| Field | Value |
|-------|-------|
| **Service** | CockroachDB Dedicated (managed CockroachDB cluster) |
| **ePHI Exposure** | **HIGH** — Persistent storage of all patient data, clinical records, billing, lab results, prescriptions |
| **Data at Rest** | AES-256 encryption, Vault-managed keys |
| **Data in Transit** | TLS 1.2+ (CockroachDB TLS) |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🔴 **Critical** |
| **Responsible** | Security Team + DBA |
| **Notes** | Primary data store — BAA must be executed before production launch. CockroachDB Labs offers standard BAA for HIPAA customers. |
| **Target Signing** | 2026-07-31 |

---

### 2. Google Cloud (GCP)

| Field | Value |
|-------|-------|
| **Service** | GKE, Cloud Storage, Cloud KMS, VPC, Load Balancing |
| **ePHI Exposure** | **HIGH** — Infrastructure hosting all services, storage of encrypted backups, key management |
| **Data at Rest** | AES-256 (CSEK via Cloud KMS / Vault) |
| **Data in Transit** | TLS 1.3, WireGuard (Cilium) |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🔴 **Critical** |
| **Responsible** | Security Team + DevOps |
| **Notes** | GCP provides a HIPAA-compliant BAA addendum. Must be signed for covered GCP services. Ensure BAA covers GKE, Cloud Storage, Cloud KMS, and VPC. |
| **Target Signing** | 2026-07-31 |

---

### 3. Elastic (ELK Stack)

| Field | Value |
|-------|-------|
| **Service** | Elasticsearch Service (Elastic Cloud) — audit log storage, log aggregation, SIEM |
| **ePHI Exposure** | **HIGH** — Audit logs contain PHI access metadata (who accessed what, when). Log ingestion from Serilog sinks. |
| **Data at Rest** | Elastic Cloud encryption at rest (AES-256) |
| **Data in Transit** | TLS 1.3 |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🔴 **Critical** |
| **Responsible** | Security Team + DevOps |
| **Notes** | Elastic Cloud offers a HIPAA-compliant BAA. Audit logs will contain PHI access patterns — BAA required prior to log ingestion in production. |
| **Target Signing** | 2026-08-07 |

---

### 4. Redis Labs (Redis Enterprise)

| Field | Value |
|-------|-------|
| **Service** | Redis Enterprise Cloud — caching, token blacklist, session store, rate limiting data |
| **ePHI Exposure** | **LOW** — No persistent ePHI storage. Cached data may transiently include patient IDs and encounter references. |
| **Data at Rest** | Redis AOF + RDB encryption (AES-256) |
| **Data in Transit** | TLS 1.2+ |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🟡 **High** |
| **Responsible** | Security Team |
| **Notes** | Redis is used for transient/in-memory data. However, cached responses may contain ePHI fragments (patient IDs). BAA recommended. |
| **Target Signing** | 2026-08-14 |

---

### 5. VMware (RabbitMQ)

| Field | Value |
|-------|-------|
| **Service** | RabbitMQ — event bus / message broker for asynchronous integration events |
| **ePHI Exposure** | **MEDIUM** — Message payloads may contain patient IDs, encounter IDs, and clinical event metadata |
| **Data at Rest** | RabbitMQ queue encryption (AES-256) |
| **Data in Transit** | AMQPS (TLS 1.2+) |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🟡 **High** |
| **Responsible** | Security Team |
| **Notes** | RabbitMQ processes integration events. While PHI in event payloads should be minimized (IDs only), BAA is prudent given potential exposure. |
| **Target Signing** | 2026-08-14 |

---

### 6. PagerDuty

| Field | Value |
|-------|-------|
| **Service** | Incident alerting and on-call management |
| **ePHI Exposure** | **NONE** — Alert notification payloads contain no ePHI. Service names and severity only. |
| **Data at Rest** | PagerDuty managed encryption (AES-256) |
| **Data in Transit** | HTTPS (TLS 1.2+) |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🟢 **Low** |
| **Responsible** | DevOps |
| **Notes** | No ePHI exposure. BAA may still be advisable for defense-in-depth. Evaluate necessity based on compliance interpretation. |
| **Target Signing** | 2026-08-28 |

---

### 7. Slack

| Field | Value |
|-------|-------|
| **Service** | Team communication, security alerts, deployment notifications |
| **ePHI Exposure** | **NONE** — Bot notifications contain only operational metadata (deployment status, alert severity). No PHI transmitted. |
| **Data at Rest** | Slack managed encryption (AES-256) |
| **Data in Transit** | HTTPS (TLS 1.2+) |
| **BAA Status** | ❌ **NOT SIGNED** |
| **Priority** | 🟢 **Low** |
| **Responsible** | DevOps |
| **Notes** | Slack explicitly states they cannot sign BAAs for their standard product. If ePHI is ever posted to Slack, it is a HIPAA breach. Ensure all automated notifications are scrubbed of PHI. |
| **Target Signing** | 2026-08-28 (or documented exemption) |

---

## Summary Matrix

| # | Vendor | ePHI Exposure | BAA Status | Priority | Target |
|---|--------|---------------|------------|----------|--------|
| 1 | CockroachDB Labs | HIGH | ❌ Not Signed | 🔴 Critical | 2026-07-31 |
| 2 | Google Cloud | HIGH | ❌ Not Signed | 🔴 Critical | 2026-07-31 |
| 3 | Elastic | HIGH | ❌ Not Signed | 🔴 Critical | 2026-08-07 |
| 4 | Redis Labs | LOW | ❌ Not Signed | 🟡 High | 2026-08-14 |
| 5 | VMware (RabbitMQ) | MEDIUM | ❌ Not Signed | 🟡 High | 2026-08-14 |
| 6 | PagerDuty | NONE | ❌ Not Signed | 🟢 Low | 2026-08-28 |
| 7 | Slack | NONE | ❌ Not Signed | 🟢 Low | 2026-08-28 |

---

## Status Legend

- **🔴 Critical** — Must sign before production launch. High ePHI exposure.
- **🟡 High** — Should sign soon. Moderate ePHI exposure or indirect access.
- **🟢 Low** — Sign when practical. Minimal or no ePHI exposure.

---

## Review Cycle

- **Frequency:** Quarterly
- **Next Review:** 2026-10-17
- **Owner:** Security Team

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-07-17 | Initial registry created | Security Team |
