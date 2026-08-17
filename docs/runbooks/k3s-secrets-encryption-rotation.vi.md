# Xoay khóa mã hóa Secret K3s

Quy trình này chỉ chạy qua workflow được bảo vệ `K3s Secrets Encryption Rotation`.
Không chạy trực tiếp trên production và không ghi token, khóa hay giá trị Secret vào evidence.

1. Xác nhận snapshot etcd mới nhất và cửa sổ bảo trì đã được phê duyệt.
2. Chạy workflow với `apply=false` để kiểm tra đầu vào.
3. Với production, chọn environment `production`, `apply=true` và environment protection bắt buộc reviewer.
4. Ansible chạy serial trên control-plane, chỉ xoay khóa một lần, re-encrypt dữ liệu cũ, restart từng server và kiểm tra `/readyz`.
5. Xác minh `k3s secrets-encrypt status` báo `Encryption Status: Enabled` và `Reencrypt Finished: true` trên mọi server.
6. Lưu evidence `k3s-secrets-encryption-rotation.json`; nếu fail, dừng rollout và dùng snapshot/rollback runbook đã phê duyệt.

Rotation không thay thế backup/restore drill. Mọi thao tác phải có audit log và đối soát Secret consumer sau khi hoàn tất.
