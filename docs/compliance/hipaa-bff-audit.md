# HIPAA Audit Trail — BFF Layer

## Audit Events

| Event | Log Level | Contains |
|-------|-----------|----------|
| BFF_AUDIT | Information | All requests: timestamp, sessionId, userId, path (redacted), status, duration |
| BFF_PHI_ACCESS | Warning | PHI-flagged requests: userId, sessionId, redacted path |

## Retention
- BFF audit logs flow to ELK stack
- PHI access logs: retained 6 years (HIPAA minimum)
- Non-PHI logs: retained 90 days

## Redaction
- Patient/encounter/lab/invoice/prescription IDs in log paths are replaced with `{redacted}`
- Full IDs available in correlation-tracked backend audit logs

## Compliance Matrix
| HIPAA Requirement | BFF Implementation |
|------------------|-------------------|
| 164.312(b) — Audit Controls | BFF_AUDIT logs all access |
| 164.312(c)(1) — Integrity | Correlation ID links BFF→backend audit |
| 164.312(d) — Person Authentication | SessionId + UserId in every audit entry |
| 164.312(e)(1) — Transmission Security | mTLS (Linkerd) + Wireguard (Cilium) |
