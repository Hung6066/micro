# His.Hope — Technical guide K3s cho developer

> Mã: DEV-K3S-001
> Đối tượng: Backend, BFF, Angular, mobile và platform developer.
> Mục tiêu: thêm service/API/workload mới mà không phá identity, secret, observability, routing hoặc deployment contract.

## 1. Kiến trúc runtime

~~~mermaid
flowchart LR
    Browser[Angular browser] --> Edge[Traefik HTTPS edge]
    Mobile[Native Android iOS] --> OIDC[Identity OIDC]
    Edge --> BFF[BFF cookie session]
    BFF --> Gateway[API Gateway]
    Gateway --> Services[Domain services]
    Services --> DB[Service database]
    Services --> Events[Outbox event bus]
    Services --> OTEL[OpenTelemetry Collector]
    OTEL --> Traces[Jaeger Tempo]
    Services --> Metrics[Prometheus]
    Nodes[K3s nodes] --> Metrics
    Nodes --> Logs[Promtail]
    Logs --> Loki[Loki object storage]
    Metrics --> Grafana[Grafana]
    Loki --> Grafana
    Traces --> Grafana
    Services --> SPIRE[SPIRE JWT SVID]
    SPIRE --> Vault[Vault production]
    Vault --> CSI[Vault CSI]
~~~

| Namespace | Trách nhiệm |
|---|---|
| his-hope | Production backend, BFF, domain services, Vault production |
| his-hope-dev | Local/rehearsal application stack |
| monitoring | Prometheus, Grafana, Loki, Jaeger, OTEL, Alertmanager |
| linkerd | Linkerd control plane |
| linkerd-viz | Linkerd metrics/UI |
| linkerd-cni | Linkerd CNI |
| spire | SPIRE server, datastore, agents, registration |
| backup | MinIO/object storage và backup jobs |
| harbor | Private registry và image signing |

Không đưa resource monitoring vào Kustomization có namespace transformer his-hope-dev. Apply observability riêng để giữ namespace và service DNS đúng.

## 2. Chuẩn workload mới

Cấu trúc tối thiểu:

    src/Services/<Service>/<Service>.Api
    src/Services/<Service>/<Service>.Application
    src/Services/<Service>/<Service>.Domain
    src/Services/<Service>/<Service>.Infrastructure
    tests/<Service>.*
    k8s/base/<service>-service.yaml
    k8s/overlays/dev/<service>-local-patch.yaml
    k8s/overlays/prod/<service>-security-patch.yaml

Metadata:

    app.kubernetes.io/name: appointment-service
    app.kubernetes.io/part-of: his-hope
    app.kubernetes.io/component: backend
    app.kubernetes.io/managed-by: kustomize

Container production phải có:

- Harbor image digest, không dùng latest.
- Non-root user và read-only root filesystem nếu hỗ trợ.
- Requests/limits, startup/readiness/liveness probe.
- ServiceAccount riêng.
- SPIFFE registration riêng.
- Vault role/path riêng nếu cần secret.
- NetworkPolicy ingress/egress tối thiểu.
- Metrics/OTLP chỉ khi endpoint thực sự tồn tại.

## 3. Runtime contract

Ứng dụng không được biết nó chạy Docker, VM hay K3s. Adapter thay endpoint, application dùng logical key thống nhất:

    SERVICE_IDENTITY_API_URL
    SERVICE_PATIENT_API_URL
    SERVICE_APPOINTMENT_API_URL
    SERVICE_CLINICAL_API_URL
    SERVICE_LAB_API_URL
    SERVICE_BILLING_API_URL
    SERVICE_PHARMACY_API_URL
    SERVICE_DASHBOARD_BFF_API_URL
    OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT
    OBSERVABILITY_PROMETHEUS_URL
    OBSERVABILITY_LOKI_URL
    OBSERVABILITY_JAEGER_URL
    OBSERVABILITY_ALERTMANAGER_URL
    ADAPTER_VAULT_ADDRESS

K3s endpoint mẫu:

    http://patient-service.his-hope.svc.cluster.local:5002
    http://otel-collector.monitoring.svc.cluster.local:4317
    https://vault-active.his-hope.svc.cluster.local:8200

Không hard-code localhost, Pod IP hoặc port của container trong business code. Binding options phải đến từ runtime contract và được validate trước startup.

## 4. Identity, OIDC và session

### 4.1 Web BFF

~~~mermaid
sequenceDiagram
    participant U as Browser
    participant B as BFF
    participant I as Identity OIDC
    participant A as API
    U->>B: Protected route
    B-->>U: Authorize PKCE
    U->>I: Passkey or MFA
    I-->>B: Authorization code
    B->>I: Exchange code verifier
    I-->>B: Access and refresh token
    B-->>U: HttpOnly secure session cookie
    U->>B: API request with cookie
    B->>B: Refresh near expiry
    B->>A: Forward authorization
    A-->>B: JSON response
    B-->>U: JSON response
~~~

Web không lưu access/refresh token trong localStorage. Chỉ coi login hoàn tất khi callback code đã exchange và session/userinfo validation pass. API route không được redirect trả HTML login page.

### 4.2 Native mobile

Android dùng Credential Manager và iOS dùng AuthenticationServices cho native passkey. OIDC vẫn là Authorization Code + PKCE qua browser/Custom Tab/ASWebAuthenticationSession. Token storage dùng Android Keystore/iOS Keychain.

Luồng MFA chuẩn:

    primary authentication
      -> passkey verification
      -> mobile approval nếu thiết bị lạ
      -> TOTP fallback
      -> OIDC code/session issued

Đóng passkey dialog không đồng nghĩa authenticated. Mobile chỉ chuyển trạng thái authenticated sau callback, token exchange và user/session validation.

### 4.3 Authorization

API mới dùng permission/facility contract chung:

    RequirePermission("patients.read")
    RequirePermission("patients.write")
    RequireFacilityScope()

| Status | Ý nghĩa |
|---|---|
| 401 | Thiếu hoặc credential/session không hợp lệ |
| 403 | Đã xác thực nhưng thiếu role/permission/facility scope |
| 404 | Route/resource không tồn tại hoặc bị ẩn có chủ đích |
| 502 | Gateway/BFF upstream không reachable |
| 5xx | Lỗi xử lý server hoặc dependency |

ProblemDetails phải có traceId, correlationId và errorCode. Không dùng 401 để che lỗi timeout/502.

## 5. Workload identity và Vault

### 5.1 SPIRE, Kubernetes auth và CSI

| Cơ chế | Dùng cho | Không dùng để |
|---|---|---|
| SPIRE JWT-SVID | Backend authenticate Vault/service mesh | Thay OIDC user login |
| Vault Kubernetes auth | CSI provider và monitoring ServiceAccount | Đưa root token vào Deployment |
| Vault CSI | Mount/sync secret runtime | Commit secret vào manifest |

Backend production dùng SPIRE identity theo namespace/service account. Monitoring CSI dùng role observability và audience vault.

### 5.2 Secret path contract

    secret/data/his-hope/observability/grafana-oidc
      client_id
      client_secret

    secret/data/his-hope/observability/alertmanager
      smtp_host
      smtp_port
      smtp_username
      smtp_password
      smtp_from
      smtp_to
      discord_webhook_url

    secret/data/his-hope/observability/object-store
      endpoint
      bucket
      region
      access_key_id
      secret_access_key

Khi thêm key phải cập nhật Vault policy, SecretProviderClass objects, secretObjects, Deployment mount/env, rotation test và validator.

### 5.3 SecretProviderClass

Production observability phải có:

    roleName: observability
    audience: vault
    vaultAddress: https://vault-active.his-hope.svc.cluster.local:8200
    vaultCACertPath: /vault/tls/ca.crt
    vaultSkipTLSVerify: "false"

CSI provider Helm values phải project vault-tls và vault-tls-ca vào /vault/tls. Kiểm tra:

    kubectl -n his-hope get ds vault-csi-csi-provider
    kubectl -n monitoring get secretproviderclass observability-secrets
    kubectl -n monitoring get secretproviderclasspodstatus

Dùng pod probe bằng ServiceAccount thật để test mount. Không đọc secret bằng kubectl get secret -o yaml trong CI output.

## 6. Kustomize và Helm

### 6.1 Kustomize

    kubectl kustomize k8s/overlays/dev > $env:TEMP\his-hope-dev.yaml
    kubectl kustomize k8s/overlays/prod > $env:TEMP\his-hope-prod.yaml

Patch rules:

- replace chỉ khi path chắc chắn tồn tại trong base;
- add khi parent/object có thể chưa tồn tại;
- target theo name/label cụ thể;
- không dùng namespace transformer chung cho resource khác namespace;
- không đưa observability/Vault/backup vào application overlay nếu chúng có lifecycle riêng.

### 6.2 Helm

    helm upgrade --install <release> <chart> -n <namespace> --create-namespace -f <values> --wait --timeout 10m
    helm -n <namespace> status <release>
    helm -n <namespace> get values <release> -a

Values production không chứa secret literal. Helm release phải có revision và rollback procedure. Với Vault CSI dùng k8s/vault/vault-csi-values-production.yaml.

## 7. Observability contract

### 7.1 Metrics

Metrics endpoint không yêu cầu user session, không chứa PHI và phải kiểm soát cardinality. Không dùng patient ID, userId hoặc raw URL có ID làm label.

    sum(rate(http_server_request_duration_seconds_count{namespace="his-hope"}[5m])) by (service)
    histogram_quantile(0.99, sum(rate(http_server_request_duration_seconds_bucket{namespace="his-hope"}[5m])) by (le, service))

### 7.2 Logs

Structured log nên có timestamp, level, service, traceId, correlationId, route, statusCode và durationMs. Redact Authorization, cookie, token, password, TOTP/passkey material, webhook và PHI không cần thiết.

### 7.3 Traces

Propagate W3C traceparent qua BFF → Gateway → service → database/messaging. Không tạo trace ID mới tại mỗi boundary khi đã có parent context.

## 8. Resilience và data consistency

API upstream phải có timeout bounded, retry giới hạn cho lỗi transient/idempotent, backoff+jitter, circuit breaker, bulkhead và correlation ID. Command không có idempotency key không được retry mù.

Event publish dùng outbox transaction. Consumer idempotent theo event ID, có DLQ/replay. Không publish event trước khi database transaction commit.

Database:

- migration/deployer account khác runtime account;
- connection pool giới hạn theo replica;
- projection/pagination cho list/search;
- facility/tenant filter tại query boundary;
- audit write không đẩy PHI vào log;
- backup/WAL restore test định kỳ.

## 9. Checklist khi thêm API mới

1. Định nghĩa request/response/error contract.
2. Thêm permission và facility scope.
3. Thêm BFF/Gateway route nếu web/mobile dùng.
4. Thêm localization key, không hard-code text.
5. Dùng timezone/locale/currency từ shared foundation.
6. Thêm validation, timeout và ProblemDetails.
7. Thêm OpenTelemetry activity/metrics tại boundary.
8. Thêm test 401, 403, 200, timeout và cancellation.
9. Cập nhật runtime contract và các adapter Compose/VM/K3s.
10. Build/test/render image digest.
11. Test Angular/mobile login, API, refresh và logout.
12. Cập nhật docs và rollback note.

## 10. Validation

    dotnet build His.Hope.sln --no-restore
    dotnet test His.Hope.sln --no-build
    kubectl kustomize k8s/overlays/dev
    kubectl apply --dry-run=server -f k8s/observability/production-secrets.yaml -o name
    git diff --check
    linkerd check
    curl.exe -fsS https://identity.<production-domain>/.well-known/openid-configuration
    kubectl -n monitoring exec deploy/prometheus -- wget -qO- http://127.0.0.1:9090/-/ready
    kubectl -n monitoring exec deploy/grafana -- wget -qO- http://127.0.0.1:3000/api/health

Build, pod health và API health chỉ là gate thành phần. Release gate phải bao gồm browser/native login, authorization, refresh/logout và API data load.

## 11. Debug decision tree

~~~mermaid
flowchart TD
    Start[Request failed] --> HTTP{HTTP status}
    HTTP -->|401| Auth[Issuer cookie token exchange session]
    HTTP -->|403| Policy[Permission role facility scope]
    HTTP -->|404| Route[BFF Gateway Service path]
    HTTP -->|502| Upstream[DNS Endpoint readiness timeout]
    HTTP -->|5xx| App[Logs trace database dependency]
    Auth --> OIDC[Discovery callback cookie domain]
    Policy --> Claims[Roles permissions cache version]
    Upstream --> Network[Linkerd NetworkPolicy DNS TLS]
    App --> Signals[Metrics logs traces rollout]
~~~

Khi UI loading vô hạn:

1. Mở Network và phân loại pending/cancelled/401/403/502.
2. Kiểm tra finalize trong Angular/mobile observable.
3. Kiểm tra BFF timeout và upstream cancellation.
4. Đối chiếu traceId ở BFF, service và collector.
5. Không sửa UI trước khi biết request nào chưa hoàn tất.

## 12. Không được làm

- Không commit secret, token, recovery key, private key hoặc webhook.
- Không gọi service bằng Pod IP hoặc port hard-code.
- Không dùng localhost trong production runtime config.
- Không thêm API chỉ ở Angular mà thiếu BFF/Gateway/security contract.
- Không tắt TLS verification để chữa production.
- Không mở NetworkPolicy toàn cluster để chữa một route.
- Không dùng root Vault token trong Pod.
- Không dùng một ServiceAccount cho nhiều security domain.
- Không coi health 200 là bằng chứng login/API business flow.
- Không đánh dấu release pass khi chưa test Angular, dashboard, admin và mobile.

