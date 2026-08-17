# Bootstrap Argo CD cho GitOps

Argo CD phải được cài ở staging trước production. Script bootstrap dùng manifest chính thức được pin version và bắt buộc người vận hành cung cấp SHA-256 đã review; script không in secret hoặc kubeconfig.

Profile standard hiện tại không tạo PVC: Redis, repo-server cache và controller
working directories dùng `emptyDir`. Đây là chủ ý để Argo không phụ thuộc
Longhorn hay disk mới trên từng VM; desired state nằm trong Git/Kubernetes API.
Bootstrap script sẽ fail-closed nếu manifest upstream tương lai thêm PVC, cho
đến khi storage owner phê duyệt một shared CSI profile.

## Chuẩn bị

1. Chọn release được hỗ trợ, đọc release notes và lấy manifest từ URL chính thức:
   `https://raw.githubusercontent.com/argoproj/argo-cd/v3.4.1/manifests/install.yaml`
2. SHA-256 đã kiểm tra cho manifest standard v3.4.1 là:
   `17F17C65A93A1D5E63822D4E6ADFC62D172E677A8F870A07A3F45F182EED657B`
   Nếu URL hoặc version thay đổi, phải tính lại và review checksum trong change record/PR.
3. Kubeconfig phải là kubeconfig staging riêng; không dùng cluster-admin CI.

## Chạy có dry-run trước

```powershell
pwsh ./scripts/bootstrap-argocd.ps1 `
  -Environment staging `
  -Kubeconfig .\artifacts\kubeconfig-staging.yaml `
  -Version v3.4.1 `
  -ManifestSha256 17F17C65A93A1D5E63822D4E6ADFC62D172E677A8F870A07A3F45F182EED657B `
  -WhatIf
```

Sau khi review output và change window, chạy lại bỏ `-WhatIf`. Chỉ sync `k8s/gitops/bootstrap` sau khi namespace `argocd` và các deployment đều Available.

Khi workflow chạy với `apply=true`, nó đợi CRD `applications.argoproj.io`
Established rồi áp dụng `k8s/gitops/bootstrap` bằng server-side apply. Vì vậy
`AppProject` và các `Application` được tạo cùng change record, thay vì cài Argo
CD xong nhưng để cluster không có GitOps owner. Với dry-run, chỉ render/kiểm
tra; không có resource nào bị mutate.

Sau khi bootstrap manifest được cài, apply/render contract và kiểm tra health
customization/retry/manual production sync:

```powershell
pwsh ./scripts/validate-argocd-bootstrap.ps1 `
  -OutputPath artifacts/evidence/argocd-bootstrap-contract.json
```

Contract này yêu cầu health handler cho Deployment, Job, Service và Linkerd
Server; mọi Application có retry backoff. Production có thể dùng auto-sync
chỉ khi Application mang `his-hope.io/auto-sync-approved: "true"` và trỏ tới
reviewed branch khác `main`; nếu không, contract vẫn fail-closed.

Repo-server có egress policy tối thiểu tới DNS và TCP/443. Trước khi bật
production auto-sync, phải kiểm tra từ chính pod repo-server:

```powershell
kubectl -n argocd exec deploy/argocd-repo-server -c argocd-repo-server -- `
  sh -c 'timeout 15 openssl s_client -connect github.com:443 -servername github.com -brief </dev/null'
```

Nếu node có thể truy cập GitHub nhưng pod timeout ở TCP/TLS, cần mở SNAT và
TCP/443 cho pod CIDR `10.42.0.0/16` trên gateway/firewall Viettel (hoặc cấp
HTTPS proxy cho repo-server). Khi probe pass, refresh Application và xác nhận
`Sync=Synced`; trạng thái `Healthy` đơn lẻ không chứng minh đã fetch Git.

Production bị chặn mặc định. Chỉ sau khi staging pass và change được phê duyệt mới chạy với kubeconfig production, thêm `-Environment production -AllowProduction`; vẫn phải review checksum và rollback plan trước khi bỏ `-WhatIf`.

## Xác nhận sau cài

```powershell
$env:KUBECONFIG = '.\artifacts\kubeconfig-staging.yaml'
kubectl get pods -n argocd
kubectl get crd applications.argoproj.io appprojects.argoproj.io
kubectl apply --server-side --dry-run=server -k k8s/gitops/bootstrap
kubectl get appproject his-hope -n argocd
kubectl get applications -n argocd
```

Production auto-sync chỉ được bật cho reviewed branch có annotation approval;
nhánh `main` vẫn fail-closed để tránh triển khai ngoài quy trình.

## Chạy qua GitHub Actions

Workflow `.github/workflows/argocd-bootstrap.yml` là đường bootstrap chuẩn:

1. Chọn protected environment `staging` hoặc `production`.
2. Đặt `KUBECONFIG_STAGING_B64` hoặc `KUBECONFIG_PRODUCTION_B64` tương ứng.
3. Giữ `apply=false` để kiểm tra checksum/dry-run; chỉ chọn `apply=true` sau
   staging validation và change approval.
4. Production chỉ chạy khi workflow được bảo vệ bởi environment approval và
   checksum manifest đã review.
