# Đánh giá capacity 5 host production

## Bằng chứng SSH read-only ngày 2026-08-06 sau reset

| Host | Hostname | CPU | RAM tổng | RAM available | Root filesystem | Free root | K3s | Ghi chú |
|---|---|---:|---:|---:|---:|---:|---|---|
| `172.16.102.7` | `master-01` | 8 vCPU | 15.6 GiB | 15.0 GiB | 100 GiB / 8% | 87 GiB | inactive | 4GiB swap bật |
| `172.16.102.8` | `master-02` | 8 vCPU | 15.6 GiB | 15.0 GiB | 100 GiB / 8% | 87 GiB | inactive | 4GiB swap bật |
| `172.16.102.9` | `node-01` | 8 vCPU | 15.6 GiB | 15.0 GiB | 100 GiB / 8% | 87 GiB | inactive | 4GiB swap bật |
| `172.16.102.10` | `node-02` | 8 vCPU | 15.0 GiB | 15.0 GiB | 100 GiB / 8% | 87 GiB | inactive | 4GiB swap bật |
| `172.16.102.12` | `node-03` | 8 vCPU | 15.6 GiB | 15.0 GiB | 100 GiB / 8% | 87 GiB | inactive | 4GiB swap bật |

Tất cả 5 host:

- Ubuntu 22.04.2 LTS, kernel cần kiểm tra lại sau reset.
- Swap 4GiB đang bật trên cả 5 host; phải tắt trước K3s.
- Root LV 100GiB, còn khoảng 87GiB trống trên cả 5 host.
- K3s, Docker và containerd đều inactive trên cả 5 host.

## Kết luận

**Năm host hiện đạt CPU/RAM/disk cơ bản cho topology 3 control-plane + 2 worker**, nhưng chưa đạt preflight vì swap còn bật và K3s chưa được cài.

## Ngưỡng đề xuất

### Topology đề xuất với 5 host

```text
.7  control-plane + etcd
.8  control-plane + etcd
.9  control-plane + etcd
.10 worker-app
.12 worker-data/observability
```

Không chạy data workload nặng trên ba control-plane.

### Capacity còn lại

- 16GiB RAM/worker phù hợp workload nhỏ/trung bình, không phù hợp toàn bộ PostgreSQL/Harbor/MinIO/observability cùng lúc.
- Cần storage replicated/CSI hoặc managed database/storage cho dữ liệu quan trọng.

## Việc phải làm trước khi cài K3s

1. Mở rộng root LV hoặc tạo/mount data disk riêng cho `/var/lib/rancher/k3s`, containerd, Harbor, MinIO và backup staging.
2. Tắt swap trên cả 5 host.
3. Quyết định control-plane có taint hay không; nếu chỉ có 3 host, cần worker riêng để chạy workload production.
4. Cài/enable K3s sau khi capacity gate đạt; hiện cả 3 service `k3s` đều inactive.
5. Chạy lại `ansible/enterprise-k3s/playbooks/00-preflight.yml` trước bootstrap.
6. Không dùng Docker làm runtime production thay cho containerd/K3s.

## Trạng thái gate

| Gate | Trạng thái |
|---|---|
| CPU | PASS cho control-plane, CONDITIONAL cho combined workload |
| RAM | CONDITIONAL |
| Disk free | FAIL |
| K3s installed/running | FAIL |
| Swap disabled | FAIL |
| Production-ready | FAIL |
