# Identity Service — runbook tích hợp external gates

Tài liệu này là checklist triển khai cho các tích hợp không thể chứng minh chỉ
bằng Docker local. Mặc định an toàn vẫn được giữ: provisioning `dry-run`, SSF,
mTLS và RADIUS tắt, device posture `observe`. Không đặt secret/token/private
key vào git, Docker Compose, VM env example hoặc admin-app; các giá trị đó phải
được cấp từ Vault/Kubernetes Secret/secret provider.

## 1. Biến môi trường chuẩn

### Google Workspace provisioning

```dotenv
PROVISIONING_MODE=dry-run             # enabled|live chỉ sau khi contract test pass
PROVISIONING_GOOGLE_WORKSPACE_ENABLED=false
PROVISIONING_GOOGLE_WORKSPACE_BASE_URL=https://admin.googleapis.com/admin/directory/v1
PROVISIONING_GOOGLE_WORKSPACE_TOKEN_URL=https://oauth2.googleapis.com/token
PROVISIONING_GOOGLE_WORKSPACE_SECRET_ID=google-workspace-service-account
PROVISIONING_GOOGLE_WORKSPACE_DELEGATED_ADMIN=admin@example.com
```

`SECRET_ID` là reference tới service-account JSON trong Vault, không phải JSON
literal. Domain-wide delegation phải chỉ cấp scope Directory users/groups
cần thiết. Kiểm tra discovery, create/update/deprovision, full-sync và retry
outbox trước khi đổi `PROVISIONING_MODE=enabled`.

### Microsoft Entra ID provisioning

```dotenv
PROVISIONING_MODE=dry-run
PROVISIONING_ENTRA_ENABLED=false
PROVISIONING_ENTRA_BASE_URL=https://graph.microsoft.com/v1.0
PROVISIONING_ENTRA_TOKEN_URL=https://login.microsoftonline.com/<tenant>/oauth2/v2.0/token
PROVISIONING_ENTRA_CLIENT_ID=<application-client-id>
PROVISIONING_ENTRA_SCOPE=https://graph.microsoft.com/.default
ENTRA_TENANT_ID=<tenant-id>
```

Client secret/certificate nằm trong secret provider. Cần kiểm tra app
permission tối thiểu, match user bằng email/group bằng name, transient retry,
mapping error dừng sync và deprovision khi binding bị gỡ.

### SSF / CAEP receiver

```dotenv
SSF_ENABLED=false
SSF_RECEIVER_URL=https://receiver.example/.well-known/ssf/events
SSF_RECEIVER_AUDIENCE=https://receiver.example/ssf
```

Receiver phải xác thực SET signature, issuer, audience, replay/nonce và trả
HTTP 2xx. SSF là tín hiệu bất đồng bộ (logout, revoke session, credential/MFA
change), không thay thế authentication hoặc synchronous authorization.

### PKI / mTLS

```dotenv
MTLS_ENABLED=false
MTLS_TRUSTED_CA_FILE=/etc/hishop/certs/client-ca.pem
```

VM mount CA tại path trên; Kubernetes dùng Secret/CSI mount cùng path. Private
CA, EKU clientAuth, chain, expiry và revocation phải được kiểm tra ở lab.
Không log certificate raw/private key; chỉ lưu thumbprint và binding metadata.

### RADIUS EAP-TLS

```dotenv
RADIUS_EAP_TLS_ENABLED=false
RADIUS_SERVER=radius.internal.example:1812
RADIUS_EAP_TLS_CA_FILE=/etc/hishop/certs/radius-ca.pem
```

RADIUS shared secret và private key chỉ ở outpost/secret provider. EAP-TLS
phải trỏ vào flow mTLS tương ứng, có server certificate và client CA riêng.

### Chrome Enterprise Device Trust

```dotenv
CHROME_VERIFIED_ACCESS_URL=https://verifiedaccess.googleapis.com
CHROME_VERIFIED_ACCESS_PROJECT_ID=<gcp-project-id>
CHROME_VERIFIED_ACCESS_CREDENTIALS_REF=vault://secret/identity/chrome-verified-access
DEVICE_POSTURE_MODE=observe          # observe|stepup|deny
```

Google Cloud service-account credential và Admin Console configuration nằm
ngoài repo. Chỉ Chrome/ChromeOS evidence hợp lệ mới được đưa vào Endpoint
Stage/policy; giữ `observe` trong pilot.

### Windows local-login lab

```dotenv
WINDOWS_DEVICE_LOGIN_LAB=https://win11-lab.example/api
WINDOWS_DEVICE_LOGIN_CREDENTIALS_REF=vault://secret/identity/windows-lab
DEVICE_POSTURE_MODE=observe
```

Đây là lab WCP/Agent, không phải production dependency. Phải kiểm tra local
login, password rotation, encrypted-directory impact, offline/RDP limitation
và rollback trước khi thử `stepup`/`deny`.

## 2. SIEM/WORM, HA/DR và FAPI — biến môi trường vận hành

Các biến dưới đây là **operator evidence contract**. Chúng không chứa secret
và không được dùng để giả lập connector thành công; validator chỉ báo READY
khi có endpoint/evidence thật.

```dotenv
# SIEM/WORM
AUDIT_APPEND_ONLY=true
AUDIT_REDACTION_ENABLED=true
AUDIT_SIEM_URL=https://siem.example/api/events
AUDIT_WORM_ENDPOINT=https://object-lock.example
AUDIT_WORM_BUCKET=his-hope-identity-audit
AUDIT_WORM_RETENTION_DAYS=2555
AUDIT_WORM_EVIDENCE_URI=https://evidence.example/siem-worm/run-<id>

# HA/DR
HA_DR_EVIDENCE_URI=https://evidence.example/ha-dr/run-<id>
HA_DR_RPO_MINUTES=5
HA_DR_RTO_MINUTES=30
DATABASE_CONTINUITY_RESTORE_DRILL_INTERVAL_HOURS=168
DATABASE_CONTINUITY_TARGET_RPO_MINUTES=5
DATABASE_CONTINUITY_TARGET_RTO_MINUTES=30

# FAPI / high-assurance OIDC
FAPI_CONFORMANCE_PROFILE=FAPI2_SECURITY
FAPI_CONFORMANCE_ISSUER=https://identity.example
FAPI_CONFORMANCE_REPORT_URI=https://evidence.example/fapi/report-<id>
FAPI_CONFORMANCE_TEST_CLIENT_ID=<non-production-test-client>
FAPI_CONFORMANCE_SECRET_REF=vault://secret/identity/fapi-test-client
```

SIEM/WORM completion requires immutable retention/object-lock proof, delivery
failure/replay drill, redaction review and correlation-id lookup. HA/DR
completion requires observed failover, backup integrity, restore drill and
measured RPO/RTO; a StatefulSet with one PostgreSQL writer is not HA evidence.
FAPI completion requires a signed external conformance report covering HTTPS
issuer, PKCE S256, PAR/JAR where applicable, sender-constrained tokens,
private_key_jwt/mTLS client auth, redirect URI and metadata requirements.

## 3. Docker, VM và Kubernetes

### Docker Compose

Đặt non-secret values trong `docker/config/compose.runtime.env` hoặc environment
overlay; secret references được inject bằng Vault/secret provider. Sau thay đổi:

```powershell
docker compose -f docker/docker-compose.yml config --quiet
docker compose -f docker/docker-compose.yml up -d --build identityservice admin-app
pwsh -NoProfile -File .\scripts\config\validate-identity-live-prerequisites.ps1
```

### VM/systemd

Render từ `deploy/vm/runtime.env.example`, mount CA vào
`/etc/hishop/certs`, cấp secret bằng provider, rồi restart unit Identity.
Không copy secret vào file example hoặc command line. Chạy static runtime
validation và health check sau restart; live systemd validation trên Windows
chỉ được phân loại `ENVIRONMENT_BLOCKED`.

### Kubernetes

Non-secret values đi qua `k8s/base/runtime-contract-configmap.yaml` và overlay
environment; Secret/CSI/Vault Agent cung cấp client secret, service-account
credential, CA bundle và evidence reference. Dùng cùng tên biến khi map vào
container (`PROVISIONING_*`, `SSF_*`, `MTLS_*`, `RADIUS_*`, `DEVICE_*`). Kiểm
tra `kubectl kustomize`, rollout readiness, Secret mount path và NetworkPolicy
trước khi bật live mode.

## 4. Bật, rollback và bằng chứng

1. Baseline: `validate-all-runtimes.ps1`, compose config, health/readiness.
2. Cấu hình secret reference và endpoint; giữ feature disabled/observe.
3. Chạy contract test trên tenant/lab, kiểm tra audit correlation và outbox.
4. Bật từng gate một; provisioning chỉ chuyển `dry-run → enabled` sau operator
   approval.
5. Rollback: provisioning về `dry-run`, SSF/mTLS/RADIUS về `false`, posture về
   `observe`; revoke test bindings và xác minh login/API an toàn.
6. Gate chỉ hoàn tất khi có HTTP contract evidence + audit event + external
   evidence URI. Thiếu tenant/PKI/lab/report là `SKIPPED`, không phải pass.

### Mapping UI/API

Admin-app chỉ hiển thị readiness, delivery health, outbox, posture preview và
audit metadata. Secret/private key/token không bao giờ trả về UI. Các mutation
đi qua Identity Service, permission/MFA/SoD/scope checks và append-only audit;
microservice PEP vẫn là điểm quyết định cuối cùng.
