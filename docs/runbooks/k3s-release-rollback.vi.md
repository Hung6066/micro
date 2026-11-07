# Runbook rollback release K3s

## Nguyên tắc

- Rollback bằng GitOps commit và image digest trước đó; không `kubectl apply` trực tiếp production.
- Không rollback destructive database migration. Chỉ rollback ứng dụng sau khi schema vẫn tương thích.
- Không ghi kubeconfig, token, mật khẩu, private key hoặc dữ liệu bệnh nhân vào evidence.

## Chuẩn bị trước promotion

1. Lưu Git SHA, image digest hiện tại, migration version và kết quả smoke test.
2. Xác nhận backup PostgreSQL/etcd gần nhất có trạng thái `completed` và có checksum.
3. Xác nhận Alertmanager/SLO không có active P0/P1.
4. Tạo promotion PR thay đổi digest trong `k8s/overlays/prod/image-digests/kustomization.yaml` và release metadata; không cập nhật file legacy `image-digests.yaml`.

## Rollback ứng dụng

1. Xác định digest ổn định ngay trước release trong Git history.
2. Tạo PR khôi phục digest, giữ nguyên migration version.
3. Chờ GitOps controller sync theo thứ tự migration → service → ingress.
4. Chạy release gate và smoke test:

```powershell
pwsh ./scripts/validate-k3s-release.ps1 -Environment prod -RequireCluster -RequirePodSecurity -OutputPath artifacts/evidence/rollback-release.json
```

5. Kiểm tra login, `/api/v1/auth/me`, dashboard, từng API domain, 401/403 và readiness.

## Khi rollback không đủ

- Nếu schema đã contract/destructive: dừng promotion, chuyển sang DR restore theo [disaster-recovery.md](disaster-recovery.md).
- Nếu image pull/signature fail: không tắt admission policy; sửa registry digest/signature và tạo PR mới.
- Nếu cluster health fail: giữ traffic ở load balancer, thu thập evidence và dùng control-plane restore đã được phê duyệt.

## Evidence bắt buộc

- release Git SHA và rollback Git SHA;
- image digest trước/sau;
- migration version;
- release gate JSON;
- login/API smoke result;
- measured recovery time và người phê duyệt.
