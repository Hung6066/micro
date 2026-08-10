# His.Hope HIPAA Compliance Documentation

> **Document Version:** 1.0  
> **Last Updated:** 2026-07-16  
> **Audience:** Security auditors, compliance officers, platform engineers  
> **Scope:** His.Hope EMR System - Full HIPAA Security Rule Coverage

---

## Table of Contents

1. [HIPAA Security Rule Overview](#1-hipaa-security-rule-overview)
2. [164.312(a)(1) - Access Control](#2-164312a1---access-control)
3. [164.312(a)(2)(i) - Unique User Identification](#3-164312a2i---unique-user-identification)
4. [164.312(a)(2)(ii) - Emergency Access Procedure](#4-164312a2ii---emergency-access-procedure)
5. [164.312(a)(2)(iii) - Automatic Logoff](#5-164312a2iii---automatic-logoff)
6. [164.312(a)(2)(iv) - Encryption and Decryption](#6-164312a2iv---encryption-and-decryption)
7. [164.312(b) - Audit Controls](#7-164312b---audit-controls)
8. [164.312(c)(1) - Integrity Controls](#8-164312c1---integrity-controls)
9. [164.312(c)(2) - Person or Entity Authentication](#9-164312c2---person-or-entity-authentication)
10. [164.312(e)(1) - Transmission Security](#10-164312e1---transmission-security)
11. [Implementation Checklist](#11-implementation-checklist)
12. [Compliance Gap Analysis](#12-compliance-gap-analysis)

---

## 1. HIPAA Security Rule Overview

The HIPAA Security Rule (45 CFR 164.312) establishes national standards for protecting electronic protected health information (ePHI). His.Hope implements a defense-in-depth approach across all administrative, physical, and technical safeguards.

### Technical Safeguards Coverage

| Standard | Section | Status | Implementation |
|----------|---------|--------|----------------|
| Access Control | 164.312(a)(1) | Implemented | RBAC, JWT, Vault, Network Policies |
| Unique User ID | 164.312(a)(2)(i) | Implemented | ASP.NET Identity, JWT sub claim |
| Emergency Access | 164.312(a)(2)(ii) | Implemented | Break-glass procedure via Admin API |
| Automatic Logoff | 164.312(a)(2)(iii) | Implemented | JWT expiry + Redis session timeout |
| Encryption/Decryption | 164.312(a)(2)(iv) | Implemented | AES-256 at rest, TLS 1.3 in transit |
| Audit Controls | 164.312(b) | Implemented | Serilog + DB Audit + ELK |
| Integrity Controls | 164.312(c)(1) | Implemented | mTLS, Digital Signatures, Audit Trail |
| Person/Auth Authentication | 164.312(d) | Implemented | JWT + Vault PKI + OAuth2/OIDC |
| Transmission Security | 164.312(e)(1) | Implemented | Linkerd mTLS, Cilium WireGuard |

---

## 2. 164.312(a)(1) - Access Control

### Requirement
Implement policies and procedures for electronic information systems that maintain ePHI to allow access only to those persons or software programs that have been granted access rights.

### Implementation

#### 2.1 RBAC (Role-Based Access Control)

**System:** IdentityService with ASP.NET Core Identity + Custom RBAC

**Roles defined:**
| Role | Permissions | Access Level |
|------|-------------|-------------|
| SystemAdmin | Full system access, user management, audit review | Critical |
| Doctor | Read/write clinical data, write orders, view patient data | High |
| Nurse | Read/write vitals, administer medications, view patient data | High |
| Receptionist | Read/write appointments, patient demographics | Medium |
| Patient | View own data only | Low |
| Auditor | Read-only audit log access | Read-only |

**Enforcement Points:**
1. **API Gateway (YARP):** Route-level authorization based on role claims
2. **Service-Level:** [Authorize(Roles = "Doctor,Nurse")] attributes on endpoints
3. **gRPC:** Server-side authorization via Linkerd ServerAuthorization
4. **Database:** Row-level security (RLS) via CockroachDB policies

**Implementation files:**
- `src/Services/IdentityService/IdentityService.Api/Program.cs`
- `src/Services/IdentityService/IdentityService.Domain/Entities/Role.cs`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/JwtAuthenticationExtensions.cs`

#### 2.2 Network Access Control

**System:** Cilium eBPF Network Policies + Linkerd Service Mesh

| Layer | Technology | Implementation |
|-------|------------|----------------|
| L3/L4 Network | CiliumNetworkPolicy | Default-deny ingress/egress per namespace |
| L7 Network | Cilium HTTP/gRPC policies | Allow specific methods/paths |
| Service Mesh | Linkerd ServerAuthorization | mTLS identity-based authorization |
| API Gateway | YARP Reverse Proxy | Path-based routing with auth delegation |

**Files:**
- `cilium/` - Network policy definitions
- `k8s/linkerd/server-authorization.yaml` - mTLS authorization rules
- `k8s/linkerd/server.yaml` - Server definitions

#### 2.3 Token Blacklisting (JWT Revocation)

**System:** Redis-backed TokenBlacklistService

- JWT tokens can be revoked immediately by jti (JWT ID)
- User-level revocation for mass invalidation (password change, compromise)
- TTL-based blacklist entries auto-cleanup after token expiry

**Files:**
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/TokenBlacklistService.cs`
- `src/Shared/Infrastructure/His.Hope.Infrastructure/Security/TokenRevocationEndpoints.cs`

---

## 3. 164.312(a)(2)(i) - Unique User Identification

### Requirement
Assign a unique name and/or number for identifying and tracking user identity.

### Implementation
- **Unique User ID:** GUID-based user IDs generated by ASP.NET Identity
- **Username:** Unique per-user (enforced at DB level)
- **Email:** Verified and unique
- **JWT Subject Claim:** sub = user GUID for all token validation
- **Session Tracking:** JWT jti (unique per token) for session correlation

**Files:**
- `src/Services/IdentityService/IdentityService.Domain/Entities/User.cs`
- `src/Services/IdentityService/IdentityService.Infrastructure/Services/JwtTokenGenerator.cs`

---

## 4. 164.312(a)(2)(ii) - Emergency Access Procedure

### Requirement
Establish and implement procedures for obtaining necessary ePHI during an emergency.

### Implementation
- **Break-Glass Account:** Pre-configured emergency admin account (stored in Vault)
- **Emergency Access Logging:** All break-glass access logged to separate immutable audit stream
- **Time-Boxed:** Emergency access tokens expire after 4 hours
- **Auto-Notification:** Slack/PagerDuty alert on emergency access use
- **Review Workflow:** Post-hoc review by security team required

---

## 5. 164.312(a)(2)(iii) - Automatic Logoff

### Requirement
Implement electronic procedures that terminate an electronic session after a predetermined time of inactivity.

### Implementation

| Parameter | Value | Configuration |
|-----------|-------|--------------|
| Access Token TTL | 15 minutes | Jwt:Expiry in config |
| Refresh Token TTL | 7 days | Refresh token record TTL |
| Absolute Session Timeout | 8 hours | Session management in IdentityService |
| Inactivity Timeout | 30 minutes | Frontend idle detection |
| Concurrent Session Limit | 5 sessions | Enforced via Redis session tracking |

---

## 6. 164.312(a)(2)(iv) - Encryption and Decryption

### Requirement
Implement a mechanism to encrypt and decrypt ePHI.

### Implementation

#### Data at Rest
| Storage Layer | Encryption | Key Management |
|---------------|------------|----------------|
| CockroachDB | AES-256 | Vault-managed keys, rotated quarterly |
| Redis Cluster | AOF + disk encryption | Vault-stored password |
| Disk (K8s nodes) | LUKS/dm-crypt | KMS-managed |
| Backups | AES-256 | Vault-managed encryption keys |

#### Data in Transit
| Transport Layer | Encryption | Protocol |
|-----------------|------------|----------|
| Service-to-Service | mTLS (Linkerd) | TLS 1.3, auto-rotated certs |
| Service-to-Database | TLS | PostgreSQL/CockroachDB TLS |
| Service-to-Redis | TLS | Redis TLS |
| Service-to-RabbitMQ | TLS | RabbitMQ TLS |
| Ingress/External | TLS 1.3 | Let's Encrypt via cert-manager |

---

## 7. 164.312(b) - Audit Controls

### Requirement
Implement hardware, software, and/or procedural mechanisms that record and examine activity in information systems that contain or use ePHI.

### Implementation

#### Audit Events Captured

| Event Type | Data Captured | Storage |
|------------|--------------|---------|
| Login/Logout | User, IP, timestamp, device, result | DB AuditLogs + Serilog |
| PHI Access | User, patient ID, resource, action | DB AuditLogs + ELK |
| Token Issuance | User, token type, expiry, family | Serilog (structured) |
| Token Revocation | User, jti, reason, admin | Redis (blacklist) + Serilog |
| Configuration Change | Admin, resource, old/new value | DB AuditLogs |
| Emergency Access | User, reason, duration, actions | Immutable audit DB |

---

## 8. 164.312(c)(1) - Integrity Controls

### Requirement
Implement policies and procedures to protect ePHI from improper alteration or destruction.

### Implementation

| Layer | Mechanism | Description |
|-------|-----------|-------------|
| Network | mTLS (Linkerd) | Prevents MITM, ensures service identity |
| Wire | WireGuard (Cilium) | Transparent encryption with integrity checks |
| Database | CockroachDB Raft | Consensus-based replication, data integrity |
| Transport | TLS 1.3 | Message authentication codes (MAC) |
| Token | RSA-SHA256 signing | Tamper-proof JWT tokens |
| Audit | SHA-256 hashing | Audit log integrity verification |
| Container | Image signing (Cosign) | Supply chain integrity |

---

## 9. 164.312(c)(2) - Person or Entity Authentication

### Requirement
Implement procedures to verify that a person or entity seeking access to ePHI is the one claimed.

### Implementation

#### Authentication Factors

| Factor | Implementation | Strength |
|--------|---------------|----------|
| Knowledge | Password (8+ chars, complexity) | Medium |
| Possession | JWT + Refresh Token | Strong |
| Inherence | TOTP/MFA (planned) | Very Strong |
| Certificate | mTLS Client Cert | Strong |
| Machine | Vault AppRole + Secret ID | Strong |

#### Authentication Flow
1. User presents credentials (username + password)
2. IdentityService validates against ASP.NET Identity
3. On success: RSA-SHA256 signed JWT issued (15m TTL)
4. Refresh token generated (7d TTL) for silent renewal
5. All subsequent requests carry JWT Bearer token
6. Token validated by each service via RSA public key
7. Token blacklist checked via Redis for revocation

---

## 10. 164.312(e)(1) - Transmission Security

### Requirement
Implement technical security measures to guard against unauthorized access to ePHI transmitted over an electronic communications network.

### Implementation

#### Encryption in Transit

| Path | Protocol | Cipher | Certificates |
|------|----------|--------|--------------|
| Client to API Gateway | HTTPS/TLS 1.3 | ECDHE + AES-256-GCM | Let's Encrypt (90d) |
| API Gateway to Services | HTTP/2 (mTLS) | TLS 1.3 | Linkerd (24h, auto-rotated) |
| Service to Service | gRPC (mTLS) | TLS 1.3 | Linkerd (24h, auto-rotated) |
| Service to Database | PostgreSQL TLS | TLS 1.2+ | Vault PKI |
| Service to Redis | Redis TLS | TLS 1.2+ | Vault PKI |
| Service to RabbitMQ | AMQPS | TLS 1.2+ | Vault PKI |
| Node to Node | WireGuard | ChaCha20-Poly1305 | Cilium-managed |

#### Service Mesh Security (Linkerd)
- **mTLS:** Automatic mutual TLS between all mesh pods
- **Identity:** Based on Kubernetes ServiceAccount
- **Certificate:** 24h validity, auto-renewed by Linkerd control plane
- **Authorization:** ServerAuthorization policies per service
- **Audit:** Linkerd Viz for mTLS status monitoring

---

## 11. Implementation Checklist

### Technical Safeguards (164.312)
- [x] Access Control (164.312(a)(1))
- [x] Unique User Identification (164.312(a)(2)(i))
- [x] Emergency Access Procedure (164.312(a)(2)(ii))
- [x] Automatic Logoff (164.312(a)(2)(iii))
- [x] Encryption and Decryption (164.312(a)(2)(iv))
- [x] Audit Controls (164.312(b))
- [x] Integrity Controls (164.312(c)(1))
- [x] Person or Entity Authentication (164.312(d))
- [x] Transmission Security (164.312(e)(1))

---

## 12. Compliance Gap Analysis

### Current Strengths
| Area | Strength |
|------|----------|
| mTLS | Full mesh-wide mTLS with 24h certificate rotation |
| Access Control | Multi-layer RBAC with JWT + Vault + Network Policies |
| Audit | Comprehensive audit trail with immutable logging |
| Secrets Management | Zero secrets in code - all via Vault |
| Encryption | End-to-end encryption (at rest, in transit, on wire) |

### Known Gaps and Remediation Plan

| # | Gap | Severity | Plan | Target |
|---|-----|----------|------|--------|
| 1 | No BAA process | High | Implement BAA management workflow | Q3 2026 |
| 2 | Manual emergency access review | Medium | Automate review workflow | Q3 2026 |
| 3 | No automated PHI discovery | Medium | Deploy data classification scanner | Q4 2026 |
| 4 | No formal risk assessment automation | Medium | Integrate risk assessment tooling | Q4 2026 |
| 5 | No patient access audit log | Low | Implement patient-facing audit | Q1 2027 |

---

> **Review Cycle:** Quarterly  
> **Next Review:** 2026-10-16  
> **Owner:** Security Team
