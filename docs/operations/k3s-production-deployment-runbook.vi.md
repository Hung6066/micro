# His.Hope — Runbook triển khai và vận hành K3s production

> Mã: OPS-K3S-001
> Đối tượng: SRE, Platform Engineer, DevOps, Security Operator
> Phạm vi: K3s production và local K3d rehearsal.

## 1. Mục tiêu và topology

Runbook này mô tả trình tự trước triển khai, bootstrap hạ tầng, rollout workload, vận hành, rotation, rollback và xử lý các lỗi production đã gặp. Không coi Pod Ready, Helm deployed hoặc kubectl apply là production sign-off nếu chưa chạy smoke test nghiệp vụ.

| Profile | Namespace chính | Mục đích |
|---|---|---|
| Local rehearsal | his-hope-dev, monitoring, backup, harbor | Rehearsal trên Windows/K3d |
| Production | his-hope, monitoring, linkerd, linkerd-viz, spire, backup, harbor | Dữ liệu thật và HA |

~~~mermaid
flowchart TD
    A[Prepare cluster] --> B[DNS TLS registry]
    B --> C[Namespaces policies]
    C --> D[Vault HA Azure auto-unseal]
    D --> E[Vault paths policies roles]
    E --> F[SPIRE Server Agent]
    F --> G[Linkerd HA CNI]
    G --> H[Database storage messaging]
    H --> I[Signed backend digests]
    I --> J[CSI observability]
    J --> K[Ingress BFF Angular mobile]
    K --> L[OIDC API smoke tests]
~~~

Namespace monitoring là stack riêng. Không đưa ../../observability vào Kustomization có namespace: his-hope-dev vì namespace transformer sẽ đổi resource monitoring và tạo conflict. Apply riêng:

    kubectl apply -k k8s/overlays/dev
    kubectl apply -k k8s/observability

## 2. Preflight

    kubectl version --client
    helm version
    vault version
    linkerd version
    cosign version
    kubectl config current-context
    kubectl get nodes -o wide
    kubectl top nodes
    kubectl auth can-i get pods --all-namespaces

Không triển khai khi context sai, node NotReady, clock/disk không ổn định, registry không pull được digest hoặc không có recovery procedure cho Vault và database.

Hostname production phải dùng HTTPS domain thật, không dùng localhost:

    identity.<production-domain>
    admin.<production-domain>
    dashboard.<production-domain>
    app.<production-domain>
    grafana.<production-domain>
    harbor.<production-domain>

    Resolve-DnsName identity.<production-domain>
    curl.exe -I https://identity.<production-domain>/.well-known/openid-configuration
    docker login harbor.<production-domain>
    pwsh -NoProfile -File scripts/validate-production-image-signatures.ps1 -RequireSigned

Production image phải là Harbor digest đã Cosign verify; không dùng latest và không coi Docker RepoDigest là chữ ký.

Secret không nằm trong Git, manifest plain text hoặc log. Các file secure chỉ dùng cho bootstrap/rotation workstation, ví dụ:

    D:\secure\his-hope\vault-production-bootstrap-token
    D:\secure\his-hope\observability-production-writer-token
    D:\secure\his-hope\grafana-oidc-client-secret
    D:\secure\his-hope\observability-minio-access-key
    D:\secure\his-hope\observability-minio-secret-key

Không gửi nội dung các file này qua chat.

## 3. Trình tự triển khai

### 3.1 Namespace, admission và NetworkPolicy

    kubectl apply -f k8s/base/namespace.yaml
    kubectl get ns his-hope monitoring linkerd linkerd-viz spire backup harbor
    kubectl get ns his-hope monitoring -o yaml

Traefik edge không inject nếu nó là ingress boundary; backend/BFF nội bộ phải tuân Linkerd và NetworkPolicy. Không chữa lỗi bằng cách mở toàn cluster.

### 3.2 Vault production HA

    kubectl apply -f k8s/production-ha/vault/vault-production.yaml
    kubectl -n his-hope rollout status statefulset/vault --timeout=300s
    kubectl -n his-hope get pods -l app.kubernetes.io/name=vault -o wide

Kiểm tra từ hostname có SAN:

    kubectl -n his-hope exec vault-0 -- sh -c 'VAULT_ADDR=https://vault-0.vault-internal.his-hope.svc.cluster.local:8200 VAULT_CACERT=/run/tls/ca.crt vault status -format=json'

Điều kiện pass: initialized=true, sealed=false, ha_enabled=true, storage_type=raft, leader ổn định và Azure auto-unseal hoạt động sau restart.

Không dùng VAULT_SKIP_VERIFY=true trong workload. Chỉ dùng tạm trên port-forward workstation khi certificate không có SAN cho 127.0.0.1.

### 3.3 KV, policy và Vault workload identity

KV v2 và Kubernetes auth chỉ enable một lần. Token reviewer phải có system:auth-delegator, CA phải là CA của Kubernetes API.

    vault secrets enable -path=secret kv-v2
    vault auth enable kubernetes

Observability paths:

    secret/data/his-hope/observability/grafana-oidc
    secret/data/his-hope/observability/alertmanager
    secret/data/his-hope/observability/object-store

Runtime role observability chỉ đọc ba path, bound vào service accounts grafana, alertmanager, observability-storage trong namespace monitoring, TTL 15 phút, max TTL 1 giờ, audience vault.

Bootstrap writer là token ngắn hạn riêng, policy:

    secret/data/his-hope/observability/*: read, create, update
    secret/metadata/his-hope/observability/*: read, list
    outside path: deny

Không mount root token vào Deployment.

### 3.4 SPIRE

    kubectl apply -k k8s/spire
    kubectl -n spire get pods -o wide
    kubectl -n spire get spiffeids,clusterspiffeids

Mỗi workload có SPIFFE ID riêng theo namespace/service account. Không dùng default hoặc wildcard cho production. Kiểm tra SVID bằng socket của agent, không chỉ kiểm tra pod Running.

### 3.5 Linkerd HA/CNI

    linkerd check
    linkerd viz check
    linkerd viz stat deploy -n his-hope
    linkerd viz edges deploy -n his-hope
    kubectl -n linkerd get deploy,pod -o wide
    kubectl -n linkerd-viz get deploy,pod -o wide

Nếu proxy không expose ổn định port 4191/metrics qua Pod IP, dùng Linkerd Viz federation/metrics path đã được phê duyệt. Không dùng unauthenticated CIDR scrape trong production.

### 3.6 Database, backup và object storage

Tách migration/deployer account khỏi runtime account. MinIO observability phải có access key riêng, chỉ được list bucket và get/put/delete trong bucket đúng.

    pwsh -NoProfile -File scripts/validate-cnpg-backup-platform.ps1 -RunBackup
    kubectl -n backup get pods,svc,pvc

Production phải có base backup, WAL archive, encryption, retention và restore test trong namespace/cluster cô lập; không restore đè live database.

### 3.7 Backend, BFF và image digest

    kubectl kustomize k8s/overlays/prod > $env:TEMP\his-hope-prod.yaml
    pwsh -NoProfile -File scripts/validate-production-image-signatures.ps1 -RequireSigned
    kubectl apply -k k8s/overlays/prod
    kubectl -n his-hope rollout status deploy/identity-service --timeout=300s
    kubectl -n his-hope get deploy,pod,svc -o wide

Rollout Identity/discovery trước Gateway/BFF, domain services rồi Angular/mobile.

### 3.8 Observability, Vault CSI và Grafana

k8s/observability/k3s-observability.yaml là profile dev một replica với local-path/RWO; không scale trực tiếp thành production. Production cần Prometheus HA/remote-write, Grafana HA + PostgreSQL, Loki distributed + S3, Jaeger/Tempo/OpenSearch HA và Alertmanager HA.

    kubectl apply -f k8s/observability/production-secrets.yaml
    kubectl apply -f k8s/observability/production-grafana-config.yaml
    kubectl apply -f k8s/observability/production-alertmanager-config.yaml
    kubectl -n monitoring get secretproviderclass observability-secrets
    kubectl -n his-hope rollout status ds/vault-csi-csi-provider --timeout=180s

CSI provider production dùng k8s/vault/vault-csi-values-production.yaml, project CA vào /vault/tls/ca.crt. SecretProviderClass phải có roleName observability, audience vault, vaultCACertPath /vault/tls/ca.crt và vaultSkipTLSVerify false.

### 3.9 Ingress và smoke test

    kubectl -n his-hope get ingress
    curl.exe -Ik https://admin.<production-domain>/auth/login
    curl.exe -Ik https://dashboard.<production-domain>/auth/login
    curl.exe -Ik https://app.<production-domain>/en/auth/login

    $d=Invoke-RestMethod https://identity.<production-domain>/.well-known/openid-configuration
    $d.issuer
    $d.authorization_endpoint
    $d.token_endpoint
    $d.jwks_uri

Sau login kiểm tra cookie BFF Secure, HttpOnly, SameSite, token exchange, refresh, logout và API patient/clinical/lab/pharmacy/billing. API không được trả HTML login page; 401/403 phải là ProblemDetails JSON.

## 4. Vận hành hằng ngày

    kubectl get nodes
    kubectl get pods -A | Select-String 'CrashLoopBackOff|ImagePullBackOff|Pending|Error'
    kubectl -n his-hope get events --sort-by=.lastTimestamp | Select-Object -Last 40
    linkerd check

Log/trace phải có service, trace ID, correlation ID, route, status và duration. Không log Authorization, cookie, access/refresh token, TOTP/passkey secret, webhook hoặc PHI không cần thiết.

Rotation chuẩn:

1. Tạo credential mới.
2. Ghi version mới vào Vault.
3. Kiểm tra CSI mount/SecretProviderClassPodStatus.
4. Reload/restart workload.
5. Smoke test.
6. Revoke credential cũ.
7. Xác nhận không còn pod dùng version cũ.

Rollback:

    kubectl -n his-hope rollout history deploy/identity-service
    kubectl -n his-hope rollout undo deploy/identity-service
    kubectl -n monitoring rollout history deploy/grafana
    kubectl -n monitoring rollout undo deploy/grafana

Rollback image phải dùng digest đã ký. Không xoá PVC/Vault Raft data trong incident nếu chưa có phê duyệt restore.

## 5. Lỗi production đã gặp

| Lỗi | Nguyên nhân gốc | Cách xử lý |
|---|---|---|
| Kustomize conflict monitoring/his-hope-dev | Observability nằm trong overlay có namespace transformer | Apply observability riêng |
| JSON patch missing resources | replace path chưa tồn tại | add nguyên object resources |
| Identity không gọi Vault | HttpClient tự tạo bỏ qua CA-aware named client | Dùng IHttpClientFactory named client |
| Vault timeout sau Linkerd | Proxy intercept 8200 hoặc egress policy | Route/skip port và NetworkPolicy đúng scope |
| Production Vault thiếu secret/ | Cluster mới chỉ initialized/unsealed | Enable KV v2, seed path |
| Token wrapper bị truyền nguyên JSON | File có root_token/recovery keys | Trích xuất nội bộ, không log |
| CSI ca.crt not found | Provider mount thiếu CA hoặc path sai | Project vault-tls + vault-tls-ca |
| CSI invalid audience | Role yêu cầu vault, token dùng audience mặc định | Thêm audience vault |
| CSI thiếu Slack/PagerDuty | SPC khai báo key chưa provision | Chỉ khai báo receiver có credential thật |
| MinIO access denied | NetworkPolicy/namespace selector không khớp K3d | Kiểm tra boundary, không mở rộng mù quáng |
| Vault Raft peer DNS fail | Headless service/rollout chưa ổn định | Kiểm tra serviceName, EndpointSlice, DNS |
| Grafana OIDC fail | Issuer/redirect URI domain mẫu hoặc HTTP | HTTPS domain thật và exact redirect URI |
| Angular/mobile 401/403/502 | Sai issuer/cookie/BFF route/upstream | Trace discovery → exchange → session → API |
| UI loading vô hạn | Không finalize, timeout hoặc error state | Kiểm tra Network, finalize và timeout |

## 6. Khẩn cấp và sign-off

    kubectl -n his-hope rollout pause deploy/identity-service
    kubectl -n his-hope rollout undo deploy/identity-service
    kubectl -n monitoring rollout undo deploy/grafana

Sau rollback chạy lại discovery, login/passkey/MFA, API 401/403/200, logout và metrics/logs/traces.

Checklist sign-off:

- [ ] Node, DNS, TLS, registry digest/Cosign pass.
- [ ] Vault HA, Azure auto-unseal, TLS, Raft peer pass.
- [ ] SPIRE identity và Linkerd mTLS pass.
- [ ] Migration/runtime DB account tách biệt.
- [ ] Backup, WAL, retention, restore test pass.
- [ ] CSI mount bằng service account production pass.
- [ ] Grafana OIDC trên HTTPS domain thật pass.
- [ ] Receiver đã provision nhận synthetic alert.
- [ ] Prometheus/Loki/Jaeger có dữ liệu từ request thật.
- [ ] Angular/mobile login, refresh, API, logout pass.
- [ ] Rollback diễn tập, không mất dữ liệu.

