# Hướng dẫn Triển khai Production — His.Hope EMR

> **Tài liệu:** OPS-DEPLOY-001
> **Version:** 1.0
> **Audience:** SRE, Platform Engineer, DevOps
> **Cập nhật:** 2026-07-16

---

## 1. Yêu cầu Tiên quyết (Prerequisites)

Trước khi triển khai production, cần cài đặt các CLI tools sau trên máy workstation triển khai:

| Tool | Version tối thiểu | Purpose |
|------|-------------------|---------|
| **kubectl** | 1.28+ | Kubernetes cluster management |
| **kustomize** | 5.0+ (built-in với kubectl) | K8s manifest patching |
| **argocd CLI** | 2.8+ | GitOps application management |
| **vault CLI** | 1.16+ | Secrets management |
| **linkerd CLI** | 2.14+ (stable-2.14.x) | Service mesh operations |
| **cilium CLI** | 1.14+ | eBPF network policy management |
| **helm** | 3.x | Package management |
| **openssl** | 1.1+ | Certificate generation |
| **jq** | 1.6+ | JSON processing |
| **cockroach CLI** | 24.1+ | Database migrations |

### 1.1 Xác thực Cluster

```bash
# Kiểm tra kết nối đến các cluster (3 regions)
kubectl config get-contexts

# Chọn context primary region
kubectl config use-context his-hope-us-east1

# Kiểm tra quyền admin cluster
kubectl auth can-i '*' '*' --all-namespaces

# Kiểm tra cluster capacity
kubectl top nodes
```

---

## 2. Trình tự Provisioning Hạ tầng

Thứ tự triển khai bắt buộc từ dưới lên. **Không được đảo thứ tự**, vì mỗi layer phụ thuộc vào layer trước.

### Step 1: Namespaces

```bash
# Apply namespace definitions (tạo tất cả namespaces)
kubectl apply -f k8s/base/namespace.yaml

# Xác nhận
kubectl get namespaces | Select-String -Pattern "his-hope|linkerd|monitoring|chaos-engineering|data-platform|vault"
```

Các namespace được tạo:
- `his-hope` — Application workloads
- `linkerd` — Service mesh control plane
- `linkerd-viz` — Service mesh dashboard
- `linkerd-multicluster` — Cross-cluster communication
- `linkerd-jaeger` — Distributed tracing
- `monitoring` — Prometheus, Grafana, Alertmanager
- `chaos-engineering` — Chaos Mesh experiments
- `data-platform` — Debezium, data pipelines
- `vault` — HashiCorp Vault HA cluster

### Step 2: Secrets (K8s Native + Vault)

```bash
# Tạo Kubernetes native secrets (postgres, redis, rabbitmq)
kubectl apply -f k8s/base/postgres-secret.yaml

# LƯU Ý QUAN TRỌNG: Sau khi apply placeholder secrets,
# phải cập nhật các giá trị thật bằng mật khẩu production.
# Dùng vault/init.sh để tự động sinh mật khẩu và lưu vào Vault.

# Tạo Vault TLS secrets từ cert đã generate
# → Đọc k8s/overlays/prod/kustomization.yaml dòng 75-89
kubectl create namespace vault --dry-run=client -o yaml | kubectl apply -f -
```

### Step 3: Vault — Khởi tạo và Unseal

Vault được cấu hình Raft-based HA với 3 nodes, Shamir seal (5 shares, threshold 3).

```bash
# Deploy Vault StatefulSet + Agent Injector + CSI Provider
kubectl apply -k k8s/overlays/prod/

# Chỉ apply Vault-specific resources nếu chưa apply toàn bộ overlay
kubectl apply -f k8s/vault/vault-statefulset.yaml
kubectl apply -f k8s/vault/vault-agent-injector.yaml
kubectl apply -f k8s/vault/vault-csi-provider.yaml

# Đợi Vault pods ready (3 pods)
kubectl wait pod -l app=vault -n vault --for=condition=Ready --timeout=300s

# Kiểm tra trạng thái Vault cluster
kubectl exec -it vault-0 -n vault -- vault status

# Port-forward để thực hiện init (nếu chưa init)
kubectl port-forward svc/vault-active -n vault 8200:8200 &

# Chạy script init từ thư mục gốc project
cd D:\AI\micro
$env:VAULT_ADDR = "https://127.0.0.1:8200"
$env:VAULT_SKIP_VERIFY = "true"
bash vault/init.sh
```

#### Vault Initialization Checklist

Sau khi `init.sh` hoàn tất, kiểm tra:

- [ ] Vault đã initialize (5 unseal keys, 3 threshold)
- [ ] Vault đã unseal (cả 3 nodes)
- [ ] Auth methods: `approle`, `kubernetes`, `jwt` enabled
- [ ] Secrets engines: `kv-v2` tại path `secret`, `transit`, `pki`, `pki_int`
- [ ] PKI Root CA + Intermediate CA đã sign
- [ ] Policies cho 7 services: `patient-service`, `identity-service`, `clinical-service`, `lab-service`, `billing-service`, `pharmacy-service`, `appointment-service`
- [ ] AppRole credentials cho từng service đã lưu vào `vault-credentials/`
- [ ] Audit device syslog enabled
- [ ] Kubernetes auth method đã configure
- [ ] JWT signing key (`transit/keys/jwt-signing`) đã tạo (RSA-4096, auto-rotate 720h)
- [ ] Database secrets (7 databases) đã seed
- [ ] RabbitMQ + Redis secrets đã seed
- [ ] mTLS certs cho tất cả services đã generate

```bash
# Kiểm tra nhanh Vault seeds
kubectl exec -it vault-0 -n vault -- vault kv list secret/his-hope/database
kubectl exec -it vault-0 -n vault -- vault kv list secret/his-hope/
```

> **QUAN TRỌNG:** Lưu root token và 5 unseal keys vào két an toàn vật lý (physical safe). Mỗi unseal key trao cho một security officer khác nhau (key custodian).

### Step 4: CockroachDB — Deploy + Migrations

```bash
# Deploy CockroachDB StatefulSet (5 replicas, 3 regions)
kubectl apply -f cockroach/config/cockroachdb-statefulset.yaml
kubectl apply -f cockroach/config/cockroachdb-service.yaml
kubectl apply -f cockroach/config/cockroachdb-init.yaml

# Đợi tất cả nodes ready
kubectl wait pod -l app=cockroachdb -n his-hope --for=condition=Ready --timeout=600s

# Kiểm tra cluster health
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach node status --insecure
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "SHOW DATABASES;"
```

#### Chạy Database Migrations (001-013)

Các migration file trong `cockroach/migrations/` chạy tuần tự:

| Migration | File | Nội dung |
|-----------|------|----------|
| 001 | `001-create-databases.sql` | Tạo 6 databases: patientdb, identitydb, appointmentdb, clinicaldb, his_hope_lab, his_hope_billing |
| 002 | `002-patient-service.sql` | Schema Patients, Allergies, Conditions, OutboxMessages |
| 003 | `003-identity-service.sql` | Schema Users, Roles, Permissions, RefreshTokens |
| 004 | `004-appointment-service.sql` | Schema Appointments, AppointmentTypes, Status tracking |
| 005 | `005-clinical-service.sql` | Schema Encounters, Vitals, Diagnoses, Procedures, SOAP Notes |
| 006 | `006-lab-service.sql` | Schema Lab orders, results, specimens |
| 007 | `007-billing-service.sql` | Schema Invoices, payments, insurance claims |
| 008 | `008-pharmacy-service.sql` | Schema Prescriptions, medications, dispensing |
| 009 | `009-seed-data.sql` | Seed data: roles, permissions, reference data |
| 010 | `010-database-roles.sql` | Database users + grants per service |
| 011 | `011-row-level-security.sql` | RLS policies for HIPAA compliance |
| 012 | `012-audit-triggers.sql` | Audit log triggers on all PHI tables |
| 013 | `013-identity-extensions.sql` | Additional identity extensions |

```bash
# Cách 1: Chạy migration qua K8s Job
kubectl apply -f cockroach/config/migration-job.yaml

# Theo dõi job
kubectl logs job/migration-job -n his-hope -f

# Kiểm tra job hoàn tất
kubectl get job migration-job -n his-hope
# Output mong đợi: COMPLETIONS 1/1

# Cách 2: Chạy thủ công (khi cần debug)
kubectl exec -it cockroachdb-0 -n his-hope -- bash /cockroach/migrations/run-migrations.sh

# Cách 3: Chạy từng migration một
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure \
  -f /cockroach/migrations/001-create-databases.sql
```

#### Kiểm tra migrations

```bash
# Kiểm tra từng database schema
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT database_name FROM [SHOW DATABASES] WHERE database_name NOT IN ('system', 'defaultdb', 'postgres');
"

# Kiểm tra tables trong patientdb
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  USE patientdb; SHOW TABLES;
"

# Kiểm tra RLS policies
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  USE patientdb; SHOW ROW LEVEL SECURITY POLICIES;
"

# Kiểm tra audit triggers
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT * FROM system.eventlog WHERE "eventType" = 'create_table' ORDER BY timestamp DESC LIMIT 10;
"
```

### Step 5: Infrastructure Services (Redis, RabbitMQ, PostgreSQL)

```bash
# Apply infrastructure StatefulSets and Services
kubectl apply -f k8s/base/redis.yaml
kubectl apply -f k8s/base/rabbitmq.yaml
kubectl apply -f k8s/base/postgres.yaml

# Đợi tất cả ready
kubectl wait pod -l app.kubernetes.io/name=redis -n his-hope --for=condition=Ready --timeout=300s
kubectl wait pod -l app.kubernetes.io/name=rabbitmq -n his-hope --for=condition=Ready --timeout=300s

# Kiểm tra Redis cluster (3 replicas, cluster mode)
kubectl exec -it redis-0 -n his-hope -- redis-cli CLUSTER NODES
kubectl exec -it redis-0 -n his-hope -- redis-cli CLUSTER INFO

# Kiểm tra RabbitMQ cluster
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl cluster_status
```

### Step 6: Application Services

```bash
# Apply toàn bộ kustomize overlay
kubectl apply -k k8s/overlays/prod/

# Hoặc apply từng service một (theo thứ tự phụ thuộc):
# 1. Identity Service (cần cho auth)
kubectl apply -f k8s/base/identity-service.yaml
kubectl wait deployment/his-hope-identity-service -n his-hope --for=condition=Available --timeout=300s

# 2. Patient Service
kubectl apply -f k8s/base/patient-service.yaml
kubectl wait deployment/his-hope-patient-service -n his-hope --for=condition=Available --timeout=300s

# 3. Appointment Service
kubectl apply -f k8s/base/appointment-service.yaml
kubectl wait deployment/his-hope-appointment-service -n his-hope --for=condition=Available --timeout=300s

# 4. Clinical Service
kubectl apply -f k8s/base/clinical-service.yaml
kubectl wait deployment/his-hope-clinical-service -n his-hope --for=condition=Available --timeout=300s

# 5. Lab + Billing + Pharmacy Services
kubectl apply -f k8s/base/lab-service.yaml
kubectl apply -f k8s/base/billing-service.yaml
kubectl apply -f k8s/base/pharmacy-service.yaml
```

### Step 7: API Gateway

```bash
# Deploy API Gateway (YARP reverse proxy)
kubectl apply -f k8s/base/api-gateway.yaml
kubectl wait deployment/his-hope-api-gateway -n his-hope --for=condition=Available --timeout=300s
```

### Step 8: Frontend + Ingress

```bash
# Deploy Angular Frontend
kubectl apply -f k8s/base/frontend.yaml
kubectl wait deployment/his-hope-frontend -n his-hope --for=condition=Available --timeout=300s

# Verify ingress/gateway routing
kubectl get svc -n his-hope api-gateway frontend

# Test internal connectivity
kubectl run test-pod --rm -it --image=busybox --restart=Never -n his-hope -- \
  wget -qO- http://api-gateway:5000/health
```

---

## 3. Linkerd Service Mesh — Cài đặt & Xác minh

### 3.1 Cài đặt Linkerd Control Plane

```bash
# Pre-flight check
linkerd check --pre

# Install CRDs (nếu chưa có)
linkerd install --crds | kubectl apply -f -

# Install Linkerd control plane với cấu hình production
linkerd install \
  --identity-trust-domain cluster.local \
  --proxy-cpu-limit 200m \
  --proxy-memory-limit 256Mi \
  | kubectl apply -f -

# Verify control plane
linkerd check

# Install Viz extension (dashboard + metrics)
linkerd viz install | kubectl apply -f -
linkerd viz check

# Install Jaeger extension (distributed tracing)
linkerd jaeger install | kubectl apply -f -
linkerd jaeger check

# Install Multicluster extension (cross-region)
linkerd multicluster install | kubectl apply -f -
linkerd multicluster check
```

### 3.2 Inject Linkerd vào Application Namespace

```bash
# Inject annotation vào namespace his-hope (đã có sẵn trong namespace.yaml)
kubectl annotate namespace his-hope linkerd.io/inject=enabled --overwrite

# Restart deployments để nhận sidecar proxy
kubectl rollout restart deployment -n his-hope

# Đợi tất cả pods có 2/2 containers (app + linkerd-proxy)
kubectl get pods -n his-hope -o wide
```

### 3.3 Xác minh mTLS Hoạt động

```bash
# Kiểm tra Linkerd edges (service-to-service với mTLS)
linkerd viz edges -n his-hope deployment

# Kiểm tra tất cả deployments có meshed (100%)
linkerd viz stat deployments -n his-hope

# Xem lưu lượng thực tế với mTLS
linkerd viz tap deploy/patient-service -n his-hope --to deploy/clinical-service --authority patient-service.his-hope.svc.cluster.local:5006
```

Output mong đợi:

```
NAME                       MESHED   SUCCESS      RPS   LATENCY_P50   LATENCY_P95   LATENCY_P99
his-hope-patient-service      1/1   100.00%   12.3rps          12ms          45ms          89ms
his-hope-identity-service     1/1   100.00%    8.1rps           8ms          35ms          67ms
his-hope-clinical-service     1/1   100.00%    5.7rps          15ms          52ms         110ms
his-hope-api-gateway          1/1   100.00%   45.2rps          10ms          40ms          78ms
```

### 3.4 Configure Service Profiles (Retries & Timeouts)

```bash
# Apply ServiceProfile definitions
kubectl apply -f k8s/linkerd/service-profiles.yaml

# Verify routes được configure
linkerd viz routes -n his-hope svc/patient-service
```

### 3.5 Configure Server Authorization (mTLS RBAC)

```bash
# Apply Server + ServerAuthorization resources
kubectl apply -f k8s/linkerd/server.yaml
kubectl apply -f k8s/linkerd/server-authorization.yaml

# Verify authorization policies
kubectl get serverauthorization -n his-hope
```

---

## 4. Canary Deployment Process

Linkerd TrafficSplit cho phép triển khai canary với tỷ lệ traffic tăng dần.

### 4.1 TrafficSplit Configuration

Các TrafficSplit resources được định nghĩa trong `k8s/linkerd/traffic-split.yaml` cho mỗi service.

```bash
# Apply TrafficSplit baseline
kubectl apply -f k8s/linkerd/traffic-split.yaml
```

### 4.2 Canary Workflow: 5% → 25% → 50% → 100%

**Ví dụ canary cho patient-service:**

```bash
# --- Giai đoạn 1: 5% traffic đến phiên bản mới (canary) ---
kubectl patch trafficsplit patient-service-split -n his-hope --type=json \
  -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "950m"},
       {"op": "replace", "path": "/spec/backends/1/weight", "value": "50m"}]'

echo "5% canary active. Monitoring for 5 minutes..."
sleep 300

# Kiểm tra metrics
linkerd viz stat trafficsplit -n his-hope patient-service-split

# So sánh error rate giữa stable và canary
linkerd viz stat deploy -n his-hope --from deploy/api-gateway | Select-String "patient"

# --- Giai đoạn 2: 25% ---
kubectl patch trafficsplit patient-service-split -n his-hope --type=json \
  -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "750m"},
       {"op": "replace", "path": "/spec/backends/1/weight", "value": "250m"}]'

echo "25% canary active. Monitoring for 5 minutes..."
sleep 300

# --- Giai đoạn 3: 50% ---
kubectl patch trafficsplit patient-service-split -n his-hope --type=json \
  -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "500m"},
       {"op": "replace", "path": "/spec/backends/1/weight", "value": "500m"}]'

echo "50% canary active. Monitoring for 10 minutes..."
sleep 600

# --- Giai đoạn 4: 100% (promote hoàn toàn) ---
kubectl patch trafficsplit patient-service-split -n his-hope --type=json \
  -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "0m"},
       {"op": "replace", "path": "/spec/backends/1/weight", "value": "1000m"}]'

echo "100% canary promoted!"
```

### 4.3 Canary Success Criteria (Tự động)

Trong pipeline Tekton CI/CD (`cicd/tekton/tasks/canary-deploy.yaml`), mỗi giai đoạn canary tự động đánh giá:

| Metric | Threshold | Source |
|--------|-----------|--------|
| Error rate (5xx) | < 0.1% increase so với stable | Prometheus |
| p99 latency | < 20% increase so với stable | Linkerd viz |
| CPU/Memory | Trong giới hạn resource requests | Metrics Server |
| Health check | 200 OK | K8s liveness probe |
| Smoke test | All pass | Newman test suite |

Nếu **bất kỳ** metric nào fail threshold → tự động rollback về 0% và gửi alert PagerDuty.

---

## 5. Rollback Procedure

### 5.1 Canary Rollback (đang trong quá trình canary)

```bash
# Rollback traffic về 100% phiên bản cũ ngay lập tức
kubectl patch trafficsplit patient-service-split -n his-hope --type=json \
  -p='[{"op": "replace", "path": "/spec/backends/0/weight", "value": "1000m"},
       {"op": "replace", "path": "/spec/backends/1/weight", "value": "0m"}]'

# Xóa canary deployment
kubectl delete deployment patient-service-v2 -n his-hope
```

### 5.2 ArgoCD Rollback (GitOps)

```bash
# Xem lịch sử deployment
argocd app history his-hope-patient-service

# Rollback về revision trước
argocd app rollback his-hope-patient-service <REVISION_NUMBER>

# Hoặc sync về trạng thái Git (HEAD)
argocd app sync his-hope-patient-service --force --prune
```

### 5.3 Manual Full Rollback (khẩn cấp)

```bash
# 1. Revert Git commit về phiên bản tốt cuối cùng
cd D:\AI\micro
git log --oneline -10  # tìm commit hash của version ổn định

# 2. Revert kustomize overlay
kubectl apply -k k8s/overlays/prod/

# 3. Restart tất cả deployments để đảm bảo sạch
kubectl rollout restart deployment -n his-hope

# 4. Verify
kubectl get pods -n his-hope
linkerd viz stat deploy -n his-hope
```

### 5.4 Database Rollback (CockroachDB)

```bash
# Restore từ full backup (xem disaster-recovery.md)
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "RESTORE DATABASE patientdb FROM 's3://his-hope-backups/patientdb?AUTH=implicit';"

# Hoặc point-in-time recovery
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "RESTORE DATABASE patientdb FROM 's3://his-hope-backups/patientdb?AUTH=implicit' AS OF SYSTEM TIME '2026-07-15T18:00:00Z';"
```

---

## 6. Health Verification Checklist

Sau khi triển khai xong, thực hiện checklist sau:

### 6.1 Infrastructure Layer

```bash
# [ ] CockroachDB cluster healthy (5/5 nodes live)
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach node status --insecure | measure-object -Line
# Expected: 6 lines (header + 5 nodes)

# [ ] Redis cluster operational (3/3 nodes, cluster_state=ok)
kubectl exec -it redis-0 -n his-hope -- redis-cli CLUSTER INFO | Select-String "cluster_state:ok"

# [ ] RabbitMQ cluster healthy (3/3 nodes, no partitions)
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl cluster_status | Select-String "partitions"

# [ ] Vault HA cluster healthy (3/3 nodes, leader elected)
kubectl exec -it vault-0 -n vault -- vault status -format=json | ConvertFrom-Json | Select-Object initialized, sealed, ha_enabled

# [ ] Linkerd control plane healthy (all checks pass)
linkerd check
```

### 6.2 Application Layer

```bash
# [ ] Tất cả deployments available (8 deployments)
kubectl get deployments -n his-hope

# Expected deployments và replicas:
# his-hope-patient-service           5/5
# his-hope-identity-service          3/3
# his-hope-clinical-service          5/5
# his-hope-appointment-service       3/3
# his-hope-lab-service               2/2
# his-hope-billing-service           2/2
# his-hope-pharmacy-service          2/2
# his-hope-api-gateway               2/2
# his-hope-frontend                  3/3
# his-hope-vault-agent-injector      2/2

# [ ] Tất cả pods có 2/2 containers (app + linkerd-proxy)
kubectl get pods -n his-hope -o custom-columns="NAME:.metadata.name,READY:.status.containerStatuses[*].ready,RESTARTS:.status.containerStatuses[*].restartCount" --no-headers | Select-String -NotMatch "2/2"

# [ ] Không có CrashLoopBackOff hoặc lỗi
kubectl get pods -n his-hope --field-selector=status.phase!=Running
# Expected: No resources found

# [ ] Linkerd meshed 100%
linkerd viz stat deploy -n his-hope -o wide
```

### 6.3 Network Layer

```bash
# [ ] Network policies active (16 policies)
kubectl get networkpolicies -n his-hope | measure-object -Line
# Expected: ~18 lines

# [ ] Cilium endpoints healthy
kubectl get ciliumendpoints -n his-hope | measure-object -Line

# [ ] Hubble flow monitoring active
hubble observe --since 1m -n his-hope --output compact
```

### 6.4 Monitoring Stack

```bash
# [ ] Prometheus scrape targets healthy
kubectl port-forward svc/prometheus-server -n monitoring 9090:9090 &
# Mở http://localhost:9090/targets → tất cả targets UP

# [ ] Grafana accessible
kubectl port-forward svc/grafana -n monitoring 3000:3000 &
# Mở http://localhost:3000 → login admin

# [ ] Alertmanager running (no firing alerts)
kubectl port-forward svc/alertmanager -n monitoring 9093:9093 &
# Mở http://localhost:9093 → #/alerts
```

---

## 7. Smoke Test Commands

### 7.1 Health Check Từng Service

```bash
# Port-forward API Gateway để test
kubectl port-forward svc/api-gateway -n his-hope 5000:5000 &

# Test health các service qua gateway
curl -s http://localhost:5000/health | ConvertFrom-Json | Select-Object status, duration

# Test từng service qua internal network
kubectl run smoketest --rm -it --image=curlimages/curl:latest --restart=Never -n his-hope -- sh -c '
  echo "=== Identity Service ===" && curl -s http://identity-service:5003/health &&
  echo "=== Patient Service ===" && curl -s http://patient-service:5002/health &&
  echo "=== Appointment Service ===" && curl -s http://appointment-service:5004/health &&
  echo "=== Clinical Service ===" && curl -s http://clinical-service:5005/health &&
  echo "=== Lab Service ===" && curl -s http://lab-service:5010/health
'
```

### 7.2 Authentication Flow Test

```bash
# Test login flow
$body = @{
    username = "admin@hishop.com"
    password = "Admin@123!"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/auth/login" `
  -Method POST -Body $body -ContentType "application/json"
$token = $response.accessToken
Write-Host "JWT Token obtained: $($token.Substring(0, 20))..."

# Test authenticated request với token
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/auth/me" `
  -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json
```

### 7.3 Patient CRUD Test

```bash
# Create patient
$patientBody = @{
    firstName = "Test"
    lastName = "Patient"
    dateOfBirth = "1990-01-01"
    gender = "Female"
    email = "test.patient@hishop.com"
    phoneNumber = "+84123456789"
    address = @{
        street = "123 Test St"
        district = "District 1"
        city = "Ho Chi Minh"
        province = "Ho Chi Minh"
        country = "Vietnam"
    }
} | ConvertTo-Json

$patientResponse = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/patients" `
  -Method POST -Body $patientBody -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $token" }

$patientId = $patientResponse.id
Write-Host "Patient created with ID: $patientId"

# Get patient
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/patients/$patientId" `
  -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json

# Search patient
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/patients/search?q=Test&page=1&pageSize=10" `
  -Headers @{ Authorization = "Bearer $token" } | ConvertTo-Json
```

### 7.4 gRPC Connectivity Test

```bash
# Kiểm tra gRPC connection qua Linkerd (dùng grpcurl port-forward)
kubectl port-forward svc/patient-service -n his-hope 5006:5006 &

grpcurl -plaintext localhost:5006 list
# Expected: patient.PatientGrpcService

grpcurl -plaintext -d '{"id": "<PATIENT_ID>"}' localhost:5006 patient.PatientGrpcService/CheckPatientExists
```

### 7.5 Event Bus Test (RabbitMQ)

```bash
# Kiểm tra RabbitMQ queues
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_queues name messages

# Kiểm tra exchanges
kubectl exec -it rabbitmq-0 -n his-hope -- rabbitmqctl list_exchanges name type | Select-String "his_hope"
# Expected: his_hope_patient, his_hope_clinical, his_hope_appointment, his_hope_lab, his_hope_billing, his_hope_pharmacy
```

### 7.6 Redis Connectivity Test

```bash
# Kiểm tra Redis cluster ping
kubectl exec -it redis-0 -n his-hope -- redis-cli PING
# Expected: PONG

# Kiểm tra cluster info
kubectl exec -it redis-0 -n his-hope -- redis-cli CLUSTER INFO | Select-String "cluster_state:ok"

# Test write/read
kubectl exec -it redis-0 -n his-hope -- redis-cli SET smoke_test "deployment_verified_$(Get-Date -Format o)"
kubectl exec -it redis-0 -n his-hope -- redis-cli GET smoke_test
```

### 7.7 Full Smoke Test Suite (Newman)

```bash
# Chạy collection smoke test qua Newman (CI/CD pipeline)
newman run cicd/tekton/tasks/smoke-test.yaml \
  --env-var base_url=http://localhost:5000 \
  --env-var admin_token=$token \
  --reporters cli,junit \
  --reporter-junit-export smoke-results.xml
```

---

## 8. Post-Deployment Verification

Sau khi triển khai và smoke test hoàn tất, xác nhận:

```bash
# [ ] SLO Metrics — Tất cả services đạt 99.9% availability
kubectl port-forward svc/grafana -n monitoring 3000:3000 &
# Mở Grafana → Dashboard "SLO Overview" → tất cả burn rates < 1.0

# [ ] Linkerd viz dashboard — tất cả edges green (không đỏ)
linkerd viz dashboard &

# [ ] Hubble UI — không có DROPPED flows bất thường
hubble observe --since 5m --verdict DROPPED -n his-hope

# [ ] Log aggregation — không có ERROR log mới trong 5 phút
# Query Kibana tại http://localhost:5601 → Discover → level: "Error" AND @timestamp > now-5m

# [ ] Chaos experiments đang chạy bình thường
kubectl get podchaos,networkchaos,stresschaos,httpchaos -n his-hope

# [ ] Backup cronjob active
kubectl get cronjob cockroachdb-backup -n his-hope
```

---

## 9. Troubleshooting Common Deployment Issues

### Pod stuck in CrashLoopBackOff

```bash
kubectl describe pod <pod-name> -n his-hope
kubectl logs <pod-name> -n his-hope -c patient-service --tail=100
kubectl logs <pod-name> -n his-hope -c linkerd-proxy --tail=50
```

### Vault agent injector not working

```bash
# Kiểm tra sidecar injected
kubectl get pods -n his-hope -o jsonpath='{.items[*].spec.containers[*].name}'

# Kiểm tra Vault agent logs
kubectl logs deployment/patient-service -n his-hope -c vault-agent

# Restart injector
kubectl rollout restart deployment/vault-agent-injector -n vault
```

### Linkerd proxy not injected

```bash
# Kiểm tra annotation
kubectl get namespace his-hope -o jsonpath='{.metadata.annotations.linkerd\.io/inject}'

# Kiểm tra webhook
kubectl get mutatingwebhookconfigurations linkerd-proxy-injector-webhook-config

# Manual inject
kubectl get deploy patient-service -n his-hope -o yaml | linkerd inject - | kubectl apply -f -
```

### CockroachDB migration failed

```bash
# Xem log migration job
kubectl logs job/migration-job -n his-hope

# Kiểm tra các migration đã applied
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "
  SELECT * FROM system.schema_migrations ORDER BY version;
"

# Rollback migration (manual)
kubectl exec -it cockroachdb-0 -n his-hope -- cockroach sql --insecure -e "DROP DATABASE IF EXISTS patientdb CASCADE;"
# Sau đó chạy lại migration job
kubectl delete job migration-job -n his-hope
kubectl apply -f cockroach/config/migration-job.yaml
```

### NetworkPolicy blocking traffic

```bash
# Kiểm tra dropped packets qua Hubble
hubble observe --since 5m --verdict DROPPED -n his-hope

# Kiểm tra policy đang áp dụng
kubectl describe networkpolicy -n his-hope | Select-String -Context 2 "Ingress|Egress"

# Test connectivity từ pod
kubectl exec -it deploy/patient-service -n his-hope -- wget -qO- --timeout=3 http://identity-service:5003/health
```
