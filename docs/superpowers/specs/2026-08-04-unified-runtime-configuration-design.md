# Unified Runtime Configuration Design

**Ngày:** 2026-08-04  
**Phạm vi:** Docker Compose, VM/systemd hoặc Windows Service, Kubernetes/K3s  
**Trạng thái:** Chờ review trước khi lập implementation plan

## Mục tiêu

Chuẩn hóa cách cấu hình và triển khai His.Hope để cùng một service binary có thể chạy trên Docker, VM và Kubernetes/K3s mà không phụ thuộc vào `localhost`, ClusterIP, tên container hoặc port host được hard-code. Một cấu hình hợp lệ phải được kiểm tra trước khi deploy và phải có thể rollback theo cùng contract.

## Nguyên tắc thiết kế

1. **Application configuration contract là nguồn chuẩn.** Runtime adapter chỉ chuyển contract sang cơ chế cung cấp biến môi trường, DNS và secret tương ứng.
2. **Service-to-service dùng service URL, không dùng host port.** Host port chỉ dành cho ingress/developer access.
3. **Không phát hiện runtime bằng hostname hoặc `localhost`.** Application chỉ nhận endpoint đã được inject.
4. **Secret tách khỏi non-secret config.** Không commit password, token, private key hoặc connection string chứa secret.
5. **Optional dependencies không chặn critical path.** Observability, Consul và metrics có timeout/fallback riêng; database, identity và message broker là dependency bắt buộc theo service.
6. **Một validator chạy trước cả ba loại deployment.** Manifest chỉ được apply sau khi contract, references và secret policy pass.

## Các phương án đã cân nhắc

### Phương án A — Canonical contract + runtime adapters (được chọn)

Giữ tên key và semantics giống nhau trong toàn hệ thống. Docker Compose đọc env file/secrets, VM đọc protected environment file hoặc Vault Agent, K3s đọc ConfigMap/SecretProviderClass/Vault CSI. Chỉ endpoint discovery và secret transport khác nhau.

**Ưu điểm:** ít thay đổi application, local dễ chạy, K3s vẫn hỗ trợ Vault/SPIRE, validator dùng chung.  
**Đánh đổi:** cần duy trì ba renderer mỏng và kiểm tra drift.

### Phương án B — Helm/Kubernetes làm nguồn cấu hình duy nhất

Dùng Helm values để sinh mọi runtime khác.

**Ưu điểm:** mạnh cho K3s.  
**Đánh đổi:** Compose/VM khó dùng, tăng coupling với Kubernetes, không phù hợp local-first.

### Phương án C — Discovery control-plane bắt buộc

Dùng Consul hoặc Dapr cho discovery/config trên cả ba runtime.

**Ưu điểm:** discovery đồng nhất.  
**Đánh đổi:** thêm control-plane bắt buộc, làm tăng failure domain và chi phí vận hành; không cần thiết để giải quyết lỗi hiện tại.

## Canonical configuration contract

Contract được biểu diễn bằng schema versioned và mapping sang environment variables. Tên biến bên dưới là public contract; giá trị secret không được lưu trong file mẫu.

### Runtime và network

| Key | Ý nghĩa | Ví dụ Docker | Ví dụ VM | Ví dụ K3s |
|---|---|---|---|---|
| `HIS_HOPE_ENVIRONMENT` | `development`, `staging`, `production` | `development` | `production` | `production` |
| `HIS_HOPE_RUNTIME` | `docker`, `vm`, `kubernetes` | `docker` | `vm` | `kubernetes` |
| `HIS_HOPE_NAMESPACE` | namespace logic | `his-hope` | `his-hope` | `his-hope-prod` |
| `HIS_HOPE_PUBLIC_ORIGIN` | origin mà browser/mobile dùng | `http://localhost:8081` | `https://app.example` | `https://app.example` |
| `HIS_HOPE_INTERNAL_SCHEME` | scheme nội bộ | `http` | `http` hoặc `https` | `http` hoặc mesh mTLS |
| `HIS_HOPE_CONFIG_VERSION` | version contract | `v1` | `v1` | `v1` |

### Identity và frontend

| Key | Ý nghĩa |
|---|---|
| `OIDC_AUTHORITY` | OIDC discovery authority, không gắn `/connect/token` thủ công |
| `OIDC_CLIENT_ID_<APP>` | client id cho `APP`, `ADMIN`, `DASHBOARD`, `MOBILE` |
| `OIDC_REDIRECT_URI_<APP>` | redirect URI đã đăng ký |
| `OIDC_POST_LOGOUT_REDIRECT_URI_<APP>` | redirect sau logout |
| `OIDC_COOKIE_DOMAIN` | chỉ bật khi domain dùng chung và policy cookie cho phép |
| `OIDC_REQUIRE_HTTPS` | bắt buộc `true` ngoài local |
| `BFF_SESSION_COOKIE_NAME` | tên cookie session server-side |
| `BFF_SESSION_STORE` | `redis` hoặc provider tương thích |

### Service endpoints

Mỗi service expose một logical key. Application config nhận URL đầy đủ, ví dụ:

```text
SERVICE_IDENTITY_URL=http://identityservice:5001
SERVICE_PATIENT_URL=http://patientservice:5002
SERVICE_APPOINTMENT_URL=http://appointmentservice:5003
SERVICE_CLINICAL_URL=http://clinicalservice:5004
SERVICE_LAB_URL=http://labservice:5010
SERVICE_BILLING_URL=http://billingservice:5020
SERVICE_PHARMACY_URL=http://pharmacyservice:5030
SERVICE_PATIENT_BFF_URL=http://patient-bff:5100
SERVICE_CLINICAL_BFF_URL=http://clinical-bff:5200
SERVICE_LAB_BFF_URL=http://lab-bff:5300
SERVICE_BILLING_BFF_URL=http://billing-bff:5400
SERVICE_PHARMACY_BFF_URL=http://pharmacy-bff:5500
SERVICE_SYSTEMDASHBOARD_BFF_URL=http://systemdashboard-bff:5700
```

Logical name và port nội bộ là invariant. Chỉ hostname/scheme thay đổi theo adapter:

- Docker Compose: DNS service name trong cùng network.
- VM: FQDN nội bộ hoặc `/etc/hosts`/DNS managed; không dùng port publish của máy khác làm contract.
- K3s: Service DNS dạng `<service>.<namespace>.svc.cluster.local` hoặc short name cùng namespace.

### Data and messaging

```text
DATABASE_IDENTITY_URL
DATABASE_PATIENT_URL
DATABASE_APPOINTMENT_URL
DATABASE_CLINICAL_URL
DATABASE_LAB_URL
DATABASE_BILLING_URL
DATABASE_PHARMACY_URL
REDIS_URL
RABBITMQ_URL
```

Các key này chứa URI không có credential. Credential được inject qua secret provider hoặc separate variables (`DATABASE_<NAME>_USERNAME`, `DATABASE_<NAME>_PASSWORD`) trong process environment mà không đưa vào log.

### Resilience and optional dependencies

```text
RESILIENCE_CONNECT_TIMEOUT_MS=1000
RESILIENCE_REQUEST_TIMEOUT_MS=5000
RESILIENCE_RETRY_COUNT=2
RESILIENCE_CIRCUIT_BREAKER_FAILURES=5
OBSERVABILITY_REQUIRED=false
CONSUL_REQUIRED=false
PROMETHEUS_REQUIRED=false
ELASTICSEARCH_REQUIRED=false
```

Nếu `*_REQUIRED=false`, service phải khởi động và API critical path phải hoạt động khi dependency không tồn tại. Nếu `true`, readiness phải fail và validator phải yêu cầu endpoint tương ứng.

## Runtime adapters

### Docker Compose

- Tạo `docker/config/contract.env.example` chỉ chứa non-secret values.
- Tạo `docker/config/compose.env` từ contract theo environment.
- `docker-compose.yml` chỉ dùng `${KEY}` hoặc `env_file`, không lặp endpoint/password giữa service blocks.
- Docker secrets/Vault Agent cung cấp secret files; local có thể dùng protected `.env` ngoài git.
- `depends_on` chỉ dùng cho startup ordering; readiness vẫn do healthcheck và application retry quyết định.

### VM: systemd và Windows Service

- Renderer tạo một file environment riêng cho từng service, quyền Linux `0640` và owner service account; Windows dùng ACL chỉ cho service identity.
- systemd dùng `EnvironmentFile=/etc/his-hope/<service>.env`; Windows Service dùng machine-level environment hoặc secret file path được ACL bảo vệ.
- Endpoint dùng internal DNS/FQDN. Nếu chạy nhiều service cùng máy, dùng loopback port đã đăng ký trong inventory, không để mỗi app tự suy luận.
- Vault Agent/secret fetcher cập nhật secret file; service restart/reload theo policy, không ghi secret vào unit file.

### Kubernetes/K3s

- ConfigMap chứa non-secret canonical values.
- SecretProviderClass/Vault CSI hoặc workload identity inject secret files/variables.
- Service DNS là nguồn endpoint; không dùng ClusterIP hoặc node port trong application config.
- NetworkPolicy, Linkerd/SPIRE và ingress là lớp runtime adapter; không làm thay đổi tên logical service.
- Overlay chỉ thay `HIS_HOPE_ENVIRONMENT`, namespace, image/digest, secret reference, hostname/scheme và replica policy.

## Secret and workload identity contract

Secret provider được lựa chọn theo runtime nhưng cùng logical paths:

```text
secret/his-hope/<environment>/database/<service>
secret/his-hope/<environment>/messaging/redis
secret/his-hope/<environment>/messaging/rabbitmq
secret/his-hope/<environment>/oidc
secret/his-hope/<environment>/tls
```

- Docker: Vault Agent hoặc Docker secret file.
- VM: Vault Agent authenticated bằng machine identity/AppRole bootstrap được rotate.
- K3s: SPIRE-issued workload identity → Vault JWT auth → Vault database/dynamic secret hoặc CSI secret file.
- Migration/deployer credential tách khỏi runtime credential ở cả ba runtime.
- Validator chặn default password, `changeme`, `postgres`, placeholder key, empty production secret và private key nằm trong ConfigMap.

## Validation and deployment flow

Một CLI/script dùng chung sẽ có các lệnh:

```text
config contract validate --environment <name> --runtime <docker|vm|kubernetes>
config contract render --environment <name> --runtime <runtime> --output <dir>
config references validate --runtime <runtime>
config secrets preflight --runtime <runtime>
deploy plan --runtime <runtime> --environment <name>
deploy smoke --runtime <runtime> --environment <name>
```

Validator phải kiểm tra:

1. Schema/version và required keys.
2. URL scheme, host, port và duplicate endpoint.
3. Cross-reference giữa gateway route, service endpoint, BFF và frontend origin.
4. Secret source tồn tại nhưng không in secret value.
5. Runtime-specific DNS/service/ingress target.
6. Health endpoint và readiness contract.
7. Config checksum được gắn vào deployment để restart khi config thay đổi.
8. Render output không chứa secret và không còn `localhost` trong production.

## Migration strategy

1. Tạo contract/schema và inventory mapping từ cấu hình hiện tại; chưa đổi behavior.
2. Di chuyển backend services và BFF sang đọc logical endpoint keys.
3. Di chuyển Docker Compose sang renderer/env file; validate stack hiện tại.
4. Di chuyển K3s base/overlays sang ConfigMap/SecretProviderClass references.
5. Tạo VM renderer và service templates cho systemd/Windows Service.
6. Bật `strict mode` ở staging: thiếu key hoặc endpoint drift làm deploy fail.
7. Production rollout theo canary, kiểm tra OIDC login/logout, API health, service-to-service calls và secret rotation.
8. Loại bỏ hard-code cũ sau khi ba runtime pass cùng bộ smoke tests.

## Verification gates

- Unit tests cho schema, renderer và forbidden secret checks.
- Compose config render/validate và startup smoke test.
- VM dry-run: systemd unit validation hoặc Windows service environment validation.
- K3s `kustomize build`, server-side apply dry-run và rollout status.
- Cross-runtime health matrix cho identity, patient, appointment, clinical, lab, billing, pharmacy và BFF.
- Browser OIDC login/logout và API smoke test cho app, admin, dashboard.
- Secret rotation/restart không làm mất service discovery hoặc session store.
- Rollback kiểm tra được bằng config/image checksum trước và sau deploy.

## Quyết định cần review

- Có chấp nhận canonical keys ở dạng environment variables như trên không.
- Có chấp nhận `OBSERVABILITY_REQUIRED=false` mặc định cho local/staging và bật `true` trong production không.
- Có chấp nhận VM dùng Vault Agent + protected environment file thay vì copy secret trực tiếp vào service unit không.
