# Runbook xử lý các gate DevSecOps còn lại

Runbook này không tự ý thay đổi production. Mỗi bước phải có change ticket,
reviewer và kubeconfig đúng cluster. Các lệnh kiểm tra dùng kubeconfig tường
minh, không dựa vào context ngầm.

## 0. Baseline bắt buộc trước change window

Kiểm tra input trước khi mở change window. Validator chỉ đọc metadata/khóa env,
không in secret value hoặc private-key content:

```powershell
pwsh scripts/validate-production-cutover-inputs.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml `
  -SecureRoot D:\secure\his-hope `
  -SshKeyPath C:\Users\Admin\.ssh\id_deploy `
  -RequireOperatorCredentials `
  -OutputPath artifacts/evidence/production-cutover-inputs.json
```

Kết quả phải là `status=pass`. Nếu dùng thao tác Ansible/control-plane, phải có
`ansible-vault-password` và private key `id_deploy`/`id_deploy.pem`; private key
có thể nằm ngoài secure root nếu truyền `-SshKeyPath` tường minh. Không dùng
public key thay cho private key. Inventory production hiện dùng các user
`master01`, `master02`, `node01`, `node02`, `node03`; load balancer dùng `root`.

Chụp baseline đã được sanitise trước mỗi lần bootstrap/rollout. Script chỉ đọc
metadata và trạng thái, tự redact các trường credential-shaped trước khi ghi
artifact:

```powershell
pwsh scripts/capture-k3s-baseline.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml `
  -OutputPath artifacts/evidence/k3s-baseline.json
```

Nếu status là `blocked`, dừng change window và sửa kubeconfig/connectivity; không
dùng baseline cũ để thay thế evidence mới.

## 1. GitOps controller

1. Kiểm tra manifest và checksum Argo CD trong PR.
2. Chạy dry-run trước:

```powershell
pwsh scripts/bootstrap-argocd.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-staging.yaml `
  -ManifestSha256 '2e6211d381b84394b5a7c98f5b285d24d48cbe2a2917c4181623d825109bd088' `
  -WhatIf
```

3. Cài vào staging, kiểm tra `argocd-server`, repo-server, application-controller
   và sync `his-hope-security-boundaries` trước khi xem xét production.
4. Production chỉ được bootstrap bằng change đã duyệt. Pipeline build/promotion
   không có quyền `kubectl apply`; riêng workflow bootstrap được bảo vệ bởi
   GitHub Environment `production`, có `apply=false` mặc định và chỉ apply sau
   approval.

## 2. Pod Security

Trước khi rollout nhãn `his-hope`, tạo hai namespace boundary bằng preflight
idempotent:

```powershell
pwsh scripts/bootstrap-k3s-security-boundaries.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml

# Chỉ sau change approval:
pwsh scripts/bootstrap-k3s-security-boundaries.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml -Apply -AllowProduction
```

`his-hope` đang chạy với `pod-security.kubernetes.io/enforce=privileged`. Không
đổi nhãn trực tiếp trước khi workload checker trả về không còn lỗi hoặc có
exception có owner và ngày hết hạn. Render và kiểm tra:

```powershell
kubectl kustomize k8s/overlays/prod > artifacts/k8s/prod.yaml
python scripts/check-restricted-workloads.py artifacts/k8s/prod.yaml
$env:KUBECONFIG='D:\AI\micro\artifacts\kubeconfig-production.yaml'
kubectl get ns his-hope --show-labels
```

Sau rollout có kiểm soát, chạy lại `scripts/validate-k3s-release.ps1 -RequirePodSecurity`.

Preflight/rollout được tự động hóa bằng:

```powershell
pwsh scripts/rollout-pod-security-production.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml

# Chỉ sau change approval:
pwsh scripts/rollout-pod-security-production.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml -Apply -AllowProduction
```

Script sẽ dừng nếu `his-hope-system` chưa tồn tại hoặc không còn là boundary
privileged cho seccomp installer. Restart workload là bước riêng để tránh
downtime ngoài kế hoạch.

Script cũng kiểm tra live Deployment/StatefulSet/DaemonSet bằng cùng rule
restricted trước khi cho phép gắn label. Vì vậy `apply=true` sẽ bị chặn nếu
workload đang chạy còn `runAsNonRoot`, `allowPrivilegeEscalation`, seccomp hoặc
resource requests/limits chưa đạt; phải rollout manifest đã review trước.

Đường chạy chuẩn qua GitHub Actions là
`.github/workflows/pod-security-production-rollout.yml`. Giữ `apply=false` để
kiểm tra boundary manifest; chỉ chọn `apply=true` trong protected environment
`production` sau khi đã duyệt change. Workflow tạo `his-hope-system`/`his-hope-data`,
đặt nhãn restricted cho `his-hope`, rồi chạy lại release validator và upload
evidence.

## 3. Signature admission

Repository đã có policy `k8s/gitops/signature-policy` dùng Sigstore Policy
Controller với public key Cosign của His.Hope. Cài controller ở staging trước;
không bật label enforcement trên production cho đến khi test pass:

Bootstrap controller chuẩn là workflow
`.github/workflows/sigstore-policy-controller-bootstrap.yml`; script dùng Helm
repo `https://sigstore.github.io/helm-charts`, chart `sigstore/policy-controller`
version `0.10.5`, `failurePolicy=Fail`, dry-run mặc định và production approval.
Không chạy Helm tự do ngoài change record vì dễ tạo drift khỏi GitOps.

Gatekeeper ExternalData vẫn là phương án thay thế nếu security team yêu cầu.
Không cài Ratify mặc định chỉ để làm xanh gate; bản thân upstream Ratify hiện
ghi rõ trạng thái experimental. Với bất kỳ provider nào, phải ghi rõ image
digest, trust root, registry endpoint, network egress và negative/positive
admission tests trong evidence.

```powershell
$env:KUBECONFIG='D:\AI\micro\artifacts\kubeconfig-staging.yaml'
kubectl get providers.externaldata.gatekeeper.sh -A
kubectl get constrainttemplates
kubectl get constraints -A
```

Chỉ khi provider và test staging đạt mới đưa constraint signature vào policy
Application production.

## 4. CSI, snapshot và PVC backup

### Elasticsearch host prerequisite

Elasticsearch không còn dùng init container `privileged` để sửa kernel sysctl.
Trước khi rollout logging trên các node được gán workload, platform owner phải
cấu hình và kiểm chứng `vm.max_map_count=262144` bằng host/Ansible hardening:

```bash
sysctl vm.max_map_count
```

Nếu giá trị thấp hơn, dừng rollout và cập nhật profile host; không mở lại
`privileged` trong namespace `monitoring`.

`local-path` không phải storage replication. Nếu các VM không còn được cấp
thêm disk, không cài Longhorn trên OS disk; storage owner phải cung cấp CSI
dùng chung từ Viettel (vSphere/vSAN CSI hoặc NFS CSI) có topology/replication
và `VolumeSnapshotClass`. Chạy gate
`scripts/validate-shared-storage-contract.ps1`, sau đó snapshot + restore trên
PVC thử trong namespace cô lập. Không đổi `storageClassName` của data
production bằng một patch chưa có restore evidence. Xem thêm
`docs/runbooks/shared-storage-csi-viettel.vi.md`.

```powershell
$env:KUBECONFIG='D:\AI\micro\artifacts\kubeconfig-staging.yaml'
kubectl get storageclass
kubectl get volumesnapshotclass.snapshot.storage.k8s.io
kubectl get crd volumesnapshots.snapshot.storage.k8s.io
```

## 5. Azure backup

Azure values và SAS chỉ đọc từ `D:\secure\his-hope\azure-production.env` ở
runtime. Không commit chúng và không đưa vào process argument. Sau khi kiểm tra
endpoint/SAS, chạy script với production context đã được reviewer xác nhận:

```powershell
pwsh scripts/bootstrap-cnpg-azure-object-store.ps1 `
  -Context '<production-context>' `
  -EnvFile 'D:\secure\his-hope\azure-production.env' `
  -Apply -AllowProduction
pwsh scripts/validate-cnpg-backup-platform.ps1 -RunBackup
```

Trước khi mở gate restore, kiểm tra object backup Azure và chạy dry-run restore
namespace cô lập (không sửa cluster khi bỏ `-Apply`):

```powershell
pwsh scripts/verify-production-backup-restore.ps1 `
  -Kubeconfig artifacts/kubeconfig-production.yaml `
  -RestoreManifest k8s/production-ha/spire-postgres-cluster.yaml `
  -TargetNamespace dr-spire-$(Get-Date -Format yyyyMMddHHmmss) `
  -OutputPath artifacts/evidence/database-restore-drill.json
```

Wrapper giữ SAS trong bộ nhớ, xác nhận có blob không rỗng dưới
`AZURE_BACKUP_PREFIX`, rồi gọi `test-cnpg-restore-drill.ps1`. Chỉ workflow được
bảo vệ mới được dùng `-Apply -AllowProduction`; namespace restore luôn phải
khác namespace production và được dọn sau drill.

## 6. Admission positive/negative test

Policy source gate không thay thế admission test thật. Workflow thủ công
`.github/workflows/admission-staging-gate.yml` yêu cầu secret
`KUBECONFIG_STAGING_B64`, chạy một Pod hợp lệ (phải được accept) và một Pod
privileged/mutable-tag/hostPath (phải bị reject) bằng
`--dry-run=server`; không tạo resource thật. Không chạy workflow này với
kubeconfig production.

Chạy local tương đương:

```powershell
pwsh scripts/test-admission-policy.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-staging.yaml `
  -RequireCluster `
  -OutputPath artifacts/evidence/admission-policy-tests.json
```

## 7. Tổng hợp bằng chứng

PR/static CI dùng `-StaticOnly` để kiểm tra manifest và storage shape mà không
đọc secret hoặc giả lập restore evidence:

```powershell
pwsh scripts/validate-storage-backup-contract.ps1 `
  -StaticOnly `
  -OutputPath artifacts/evidence/storage-backup-static.json
pwsh scripts/validate-dr-evidence.ps1 `
  -StaticOnly `
  -OutputPath artifacts/evidence/dr-evidence-static.json
```

Production gate phải dùng strict mode với secure env và evidence runtime; không
dùng `-StaticOnly` để thay thế restore drill. DR evidence strict mode:

```powershell
pwsh scripts/validate-storage-backup-contract.ps1 `
  -SecureEnvFile D:\secure\his-hope\azure-production.env `
  -OutputPath artifacts/evidence/storage-backup-contract.json
pwsh scripts/validate-dr-evidence.ps1 `
  -OutputPath artifacts/evidence/dr-evidence-contract.json
pwsh scripts/validate-k3s-remaining-gates.ps1 `
  -Kubeconfig D:\AI\micro\artifacts\kubeconfig-production.yaml `
  -InventoryPath ansible/enterprise-k3s/inventory/production.yml `
  -SshKeyPath C:\Users\Admin\.ssh\id_deploy `
  -OutputPath artifacts/evidence/remaining-gates.json
```

`validate-dr-evidence.ps1` mặc định chỉ chấp nhận evidence `pass` được thực
đo trong vòng 168 giờ gần nhất (`-MaxEvidenceAgeHours 168`) và từ chối
timestamp ở tương lai. Không được sao chép lại report cũ để vượt qua go-live.

`blocked` là trạng thái chưa có evidence hoặc cần thao tác của platform owner;
không được đổi thành `pass` bằng cách bỏ qua kiểm tra.

## Production gate qua GitHub Environment

Workflow thủ công `.github/workflows/k3s-production-go-live-gate.yml` dùng
protected environment `production` và chỉ đọc cluster. Cấu hình hai secret
ở dạng base64, không commit plaintext:

- `KUBECONFIG_PRODUCTION_B64`: kubeconfig production có quyền đọc cần thiết.
- `AZURE_PRODUCTION_ENV_B64`: nội dung `azure-production.env` đã được redacted
  khỏi log và chỉ được ghi vào file tạm trong runner.
- `ANSIBLE_SSH_PRIVATE_KEY`: khóa deploy chỉ dùng cho audit read-only systemd trên
  ba control-plane; workflow không restart service và luôn xóa file tạm.

Workflow tự dọn file tạm, upload evidence, và fail-closed nếu còn Pod Security,
storage, GitOps, backup hoặc restore gate chưa đạt.

## Cutover tuần tự trong change window

Chỉ dispatch các workflow dưới đây sau khi đã ghi change reference và có owner
được phê duyệt. Mỗi bước phải lưu artifact/evidence và dừng ngay khi gate
không pass; không chạy song song các bước có thể thay đổi workload hoặc PVC:

1. `longhorn-storage-bootstrap.yml` với `apply=true`; xác nhận StorageClass,
   VolumeSnapshotClass và 3 replica healthy.
2. `sigstore-policy-controller-bootstrap.yml` với `apply=true`; chạy positive/
   negative admission probe và xác nhận webhook `failurePolicy=Fail`.
3. `argocd-bootstrap.yml` với `apply=true`; xác nhận Applications healthy và
   manual sync policy trước khi promotion.
4. `database-continuity-pvc-migration.yml` với `apply=true`; giữ PVC cũ,
   kiểm tra checksum và chạy restore drill cô lập.
5. Mở promotion PR digest-only, merge qua protected branch, rồi để Argo sync
   migration wave 20 → Deployment wave 30 → Ingress wave 40.
6. `pod-security-production-rollout.yml` với `apply=true`; chỉ tiếp tục khi
   live revision đã synchronized và restricted preflight báo
   `TOTAL_NONCOMPLIANT_CONTAINERS=0`.
7. Chạy năm DR workflows còn thiếu evidence, sau đó chạy
   `k3s-production-go-live-gate.yml` để tổng hợp. Go-live chỉ đạt khi mọi
   step có outcome `success`; `blocked`, `unavailable` hoặc `skipped` đều là
   dừng release.

Không dùng `kubectl apply` thủ công để thay thế các workflow trên. Rollback
phải quay về Git commit/digest đã review; không rollback schema destructive và
không xóa PVC nguồn trước khi có restore/rollback evidence.

## Signature admission controller

Repository chọn Sigstore Policy Controller cho image signature admission.
Source contract nằm ở `k8s/gitops/signature-policy` và namespace production/
staging đã opt-in bằng `policy.sigstore.dev/include=true`. Bootstrap chỉ chạy
qua `.github/workflows/sigstore-policy-controller-bootstrap.yml`, pin chart
`0.10.5`, `failurePolicy=Fail`, dry-run mặc định và production approval.
Không coi source contract là runtime pass cho đến khi webhook, CRD và positive/
negative admission test trên staging đều thành công.

## CSI/replicated storage

`local-path` không được coi là storage production/DR. Workflow
`.github/workflows/longhorn-storage-bootstrap.yml` chuẩn bị Longhorn chart
`1.12.0`, đặt replica mặc định là 3 và tạo `VolumeSnapshotClass` với
`deletionPolicy: Retain`. Chỉ chạy `apply=true` sau khi xác nhận mỗi node có
đĩa dành riêng, iSCSI/mount propagation/NFS prerequisites và đã có backup
target. Trước khi Helm apply, mọi node production phải được gắn nhãn
`his-hope.io/longhorn-data-ready=true` sau khi lưu bằng chứng `lsblk`, mount
path và trạng thái iSCSI; script sẽ fail-closed nếu thiếu bất kỳ node nào.
Sau khi cài, phải migrate PVC theo từng workload và chạy restore drill;

Sau khi hoàn tất kiểm tra trên từng máy, dùng wrapper đã được bảo vệ để ghi
nhận readiness (không dùng nhãn này để thay thế kiểm tra vật lý):

Chạy audit read-only trước:

```bash
ansible-playbook -i ansible/enterprise-k3s/inventory/production.yml \
  ansible/enterprise-k3s/playbooks/25-validate-storage-prerequisites.yml \
  --private-key "$ANSIBLE_SSH_PRIVATE_KEY"
```

Playbook phải pass trên cả 5 node và chỉ kiểm tra; nó không format, mount hay
khởi động dịch vụ. Lưu output vào change record trước khi gắn nhãn.

Trong change window đã duyệt, wrapper chạy lại audit và chỉ sau khi audit pass
mới gắn label cho toàn bộ node:

```powershell
pwsh .\scripts\prepare-longhorn-nodes.ps1 `
  -Environment production `
  -Kubeconfig .\artifacts\kubeconfig-production.yaml `
  -Inventory .\ansible\enterprise-k3s\inventory\production.yml `
  -SshKeyPath $env:USERPROFILE\.ssh\id_deploy `
  -Apply -AllowProduction
```

Không dùng `kubectl label` thủ công thay cho wrapper trong production.

```powershell
$env:KUBECONFIG='artifacts/kubeconfig-production.yaml'
kubectl label node <node-name> his-hope.io/longhorn-data-ready=true --overwrite
kubectl get nodes -L his-hope.io/longhorn-data-ready
```

Không được gắn nhãn nếu node chưa có disk data riêng, filesystem/mount path
được kiểm soát, `iscsid`/mount propagation phù hợp và bằng chứng đã lưu trong
change record. Xóa nhãn khi disk/mount bị thay đổi:

```powershell
kubectl label node <node-name> his-hope.io/longhorn-data-ready-
```
 không đổi `storageClassName` hàng loạt trong cùng một release.

### Migration `database-continuity-backups`

Go-live validator sẽ fail nếu PVC hiện hữu vẫn là `local-path` nhưng overlay đã
đổi sang `longhorn`; `storageClassName` của PVC đã bind là immutable. Quy trình
được duyệt phải theo thứ tự sau:

1. Dừng lịch/worker database-continuity và xác nhận không còn backup đang ghi.
2. Tạo PVC Longhorn mới với tên tạm, giữ nguyên PVC `local-path` cũ; không xoá
   hoặc patch PVC cũ.
3. Copy dữ liệu backup sang PVC mới hoặc restore từ Azure/Velero, rồi kiểm tra
   checksum và đọc thử toàn bộ manifest backup.
4. Chạy isolated restore drill, ghi `longhorn-snapshot-restore.json` và
   `database-restore-drill.json` với RTO/RPO đo được.
5. Đổi `claimName` của Deployment trong một PR GitOps riêng, rollout có
   approval, kiểm tra `/health`, backup mới và restore từ target mới.
6. Giữ PVC cũ ở trạng thái bảo toàn cho tới khi có rollback evidence và sign-off
   của data owner; không dùng `kubectl patch storageClassName`.

`kubectl diff` và `storage-class-drift` phải pass trước khi mở promotion PR.

## Harbor TLS trust trong CI

Harbor phải dùng CA chain được tin cậy bởi GitHub runner. Không commit PEM vào
repository và không tắt TLS verification. Endpoint public hiện tại
`harbor.myduchospital.com:443` được ký bởi `His.Hope Internal Intermediate CA`
và đã được xác thực bằng `D:\secure\his-hope\vault_pki_ca_chain.pem`.
Nếu CI vẫn dùng registry legacy `harbor.his-hope.local:9443`, phải cung cấp
CA đúng với certificate đang phục vụ endpoint đó (hiện inventory là
`D:\secure\his-hope\his_hope_ca.pem`). Không dùng một CA khác hostname.
Lưu chain tương ứng dưới dạng GitHub Environment
secret `HARBOR_CA_CHAIN_B64` trong environment `production` (áp dụng cho cả
`container-release.yml` và `gitops-release-promotion.yml`). Tạo giá trị base64 trên
máy quản trị bằng PowerShell:

```powershell
$pem = [IO.File]::ReadAllBytes('D:\secure\his-hope\vault_pki_ca_chain.pem')
[Convert]::ToBase64String($pem)
```

Sau đó dán kết quả vào **Settings → Environments → production → Environment
secrets → HARBOR_CA_CHAIN_B64**. Workflow sẽ giải mã vào file tạm, kiểm tra
định dạng X.509, cài vào trust store của runner rồi mới login/pull/verify
Cosign. Secret trống hoặc PEM hỏng làm workflow dừng ngay trước khi build hoặc
promotion.
