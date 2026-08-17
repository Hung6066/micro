# SPIRE native trong K3s

## Mục tiêu

K3s dùng SPIRE làm workload identity plane cho backend. SPIRE không thay thế
OIDC/Passkey/MFA của người dùng; Angular, dashboard và mobile vẫn đăng nhập
qua Identity Service/BFF. SPIRE thay thế credential tĩnh giữa service, Vault và
database.

## P0 đã triển khai

- SPIRE Server `1.15.2` trong namespace `spire`.
- PostgreSQL datastore `spiredb`, role riêng `spire_server`.
- Server key material trên PVC `data-spire-server-0`.
- SPIRE Agent DaemonSet chạy trên mọi K3s node.
- Kubernetes PSAT node attestation, cluster name `his-hope-k3s`.
- Kubernetes workload attestor theo namespace/service account.
- NetworkPolicy cho phép riêng namespace `spire` tới PostgreSQL datastore trên
  `5432`; validator kiểm tra cả policy và lỗi datastore trong cửa sổ runtime.
- Không dùng Docker socket, Docker attestor, join token hoặc static Vault token.
- Bundle tự động publish vào ConfigMap `spire-bundle`.

Manifest nằm tại `k8s/spire/`. Secret `spire-postgres` chỉ tạo runtime, không
commit vào Git.

## Triển khai

```powershell
kubectl apply -k k8s/spire
kubectl rollout status statefulset/spire-server -n spire --timeout=300s
kubectl rollout status daemonset/spire-agent -n spire --timeout=300s
pwsh -File scripts/bootstrap-spire-k3s.ps1
pwsh -File scripts/validate-spire-only-k3s.ps1
pwsh -File scripts/validate-linkerd-spire-mtls-k3s.ps1
```

## Kiểm tra SVID

```powershell
kubectl apply -f k8s/spire/svid-test-job.yaml
kubectl wait --for=condition=complete job/spire-svid-test -n spire --timeout=120s
kubectl logs job/spire-svid-test -n spire
```

Kết quả phải có SPIFFE ID:

```text
spiffe://his-hope.local/ns/spire/sa/spire-test
```

## P1 — Vault JWT-SVID bridge — đã validate local

P1 đã chạy trong K3s local bằng SPIRE JWT-SVID trực tiếp qua Workload API socket,
không dùng Docker socket hoặc static Vault token:

```text
SPIRE JWT-SVID -> Vault JWT auth mount `jwt-spiffe` -> Vault role/policy
```

Đã tạo role riêng cho 7 service với `bound_subject` là SPIFFE ID chính xác,
`bound_audiences=vault`, `user_claim=sub`, TTL 15 phút. Mỗi workload có sidecar
fetcher đọc `/run/spire/sockets/agent.sock`, ghi JWT vào `emptyDir` RAM bằng file
tạm rồi rename atomic, mode `0440`, group `1654`.

Runtime evidence đã PASS:

- 7 backend deployment đều `2/2 Running` sau rollout tuần tự.
- Mỗi JWT-SVID login được vào Vault `auth/jwt-spiffe`.
- Rotation sau restart workload sinh token mới và token mới login được Vault.
- Vault client token revoke rồi lookup lại bị từ chối.

Các workload backend trong overlay dev/prod không còn khai báo Kubernetes Vault
auth, projected Vault token hoặc `Vault__JwtTokenFile`; chúng chỉ dùng
`Vault__AuthMethod=spiffe-jwt` và JWT-SVID file. Mount `auth/kubernetes` trong
Vault chỉ được giữ tạm cho job/operator chưa chuyển đổi, không được dùng làm
fallback im lặng cho workload đã khai báo `spiffe-jwt`.

## P2 — service-to-service mTLS — local validation đạt

P2 dùng X509-SVID qua Workload API để xác thực caller bằng SPIFFE ID. Linkerd
là mTLS data-plane được chọn. Trên K3s/k3d local, CNI phải ghi vào
`/var/lib/rancher/k3s/data/cni` và `/var/lib/rancher/k3s/agent/etc/cni/net.d`;
manifest mặc định của Linkerd nhắm `/opt/cni/bin` và `/etc/cni/net.d` nên có
thể báo Running nhưng không redirect traffic. Chạy
`scripts/configure-linkerd-cni-k3s.ps1` trước rollout. Validator đã pass cả 3
node và 7 backend pod đã được inject `linkerd-network-validator` cùng
`linkerd-proxy`, cả hai init state đều Ready. Backend listener đã được chuẩn
hóa bind IPv4 `IPAddress.Any` để proxy có thể kết nối pod backend. Policy
`allow-linkerd-backend-mesh` mở đúng proxy path `4140` egress và `4143`
ingress; policy ứng dụng vẫn kiểm soát các cổng service thật. Smoke identity
-> 6 backend service qua Linkerd trả `200`, proxy metrics ghi
`request_total ... tls="true"` và SPIFFE service-account identity. Đã scale
7 backend lên 2 replica, xóa một patient pod và xác nhận replica còn lại tiếp
tục trả `200`; restart một SPIRE Agent cũng không làm gián đoạn smoke.
Angular/mobile không truy cập Workload API trực tiếp.

## Cutover toàn diện, không quay lại flow cũ

1. **P0 — identity plane:** SPIRE Server/Agent native trong K3s, PostgreSQL
   datastore, PSAT node attestation và workload entries theo namespace/service
   account.
2. **P1 — secret plane:** mỗi service nhận JWT-SVID từ Workload API, đăng nhập
   Vault `jwt-spiffe`, lấy database credential động; rotation/revoke phải pass
   trước khi chuyển service kế tiếp.
3. **P2 — network plane:** Linkerd CNI + X509-SVID mTLS, policy theo SPIFFE ID,
   timeout/retry/circuit breaker và multi-replica failover.
4. **Legacy removal:** xoá projected Kubernetes Vault token, Kubernetes
   auth role/policy, static connection password và Docker-socket attestor sau
   khi validator quét render dev/prod không còn legacy marker.
5. **Application boundary:** Angular/dashboard/mobile vẫn gọi BFF/OIDC; SPIRE
   không thay thế user login, MFA, passkey hay session cookie.

## Gate bắt buộc trước production

- Render dev/prod không có `Vault__AuthMount=kubernetes`,
  `Vault__JwtTokenFile` hoặc `serviceAccountToken` cho backend.
- 7 service có đúng SPIFFE ID, JWT-SVID login Vault thành công và token file
  mode `0440`.
- Linkerd cấp X509-SVID; smoke HTTP/gRPC qua proxy trả `2xx`; Authorization
  chặn SPIFFE ID không hợp lệ.
- Scale tối thiểu 2 replica, xóa một pod, call tiếp tục thành công; rotation/
  revoke không lộ secret và vẫn có audit.
- Production overlay render/validate được bằng đúng lệnh deploy; Vault, SPIRE
  PostgreSQL và backup/restore phải là runtime thật.

## Production gates

- Server HA tối thiểu 3 replica, PostgreSQL HA và backup/restore đã kiểm thử.
- CA/JWT key rotation không làm mất SVID hợp lệ trong cửa sổ chuyển tiếp.
- Vault JWT auth validate `iss`, `sub`, `aud`, signature và expiry.
- Revoke SVID chặn request mới.
- Dynamic database lease tự rotate và connection pool drain credential cũ.
- mTLS reject caller không có X509-SVID hợp lệ.

### Production gate còn mở

- Production phải chạy lại `linkerd check`, mTLS traffic test và
  multi-replica/failover sau khi apply production overlay.
- SPIRE Server hiện là 1 replica local; production phải chuyển PostgreSQL
  datastore thành HA và Server tối thiểu 3 replica trước khi failover sign-off.
- Vault database dynamic roles và migration/deployer account phải được bật riêng;
  không cho service dùng credential migration trong runtime.
