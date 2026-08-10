# Hướng Dẫn Di Chuyển His.Hope: K3s → GKE

## 1. Khi Nào Nên Nâng Cấp

| Yếu Tố Kích Hoạt | Tín Hiệu |
|-------------------|----------|
| Lưu lượng > 1000 req/s duy trì | K3s single-node bottleneck |
| Cần multi-zone HA (> 99.9% uptime) | GKE regional cluster |
| > 3 bệnh viện kết nối | Cần quản lý tập trung |
| Yêu cầu Audit/Compliance | SOC2/HIPAA cần managed K8s |
| Team > 5 developers | Cần RBAC, namespaces, quotas |

## 2. So Sánh: K3s vs GKE

| Tính Năng | K3s | GKE |
|-----------|-----|-----|
| Control plane | Tự quản lý | Google quản lý |
| Node scaling | Manual / DIY | Auto node provisioning |
| Load balancer | MetalLB / NodePort | GCP Cloud Load Balancer (tự động) |
| Storage | Local path / Longhorn | GCP Persistent Disk / Filestore |
| DNS | CoreDNS / manual | Cloud DNS tích hợp |
| SSL certs | cert-manager + self-signed | Google Managed Certificates |
| Logging | Local / DIY ELK | Cloud Logging tích hợp sẵn |
| RBAC | Cơ bản | GCP IAM tích hợp |

## 3. Checklist Trước Khi Di Chuyển

- [ ] Backup CockroachDB: `cockroach backup INTO 'gs://his-hope-backups/pre-migration'`
- [ ] Export tất cả K8s secrets: `kubectl get secrets -A -o yaml > secrets-backup.yaml`
- [ ] Snapshot PVCs nếu có dữ liệu stateful ngoài CockroachDB
- [ ] Cập nhật DNS TTL về 60s (để chuyển đổi nhanh)
- [ ] Chuẩn bị script rollback: K3s backup + lệnh restore

## 4. Các Bước Di Chuyển

### Bước 1: Tạo GKE Cluster

```bash
gcloud container clusters create his-hope-prod \
  --region us-east1 \
  --num-nodes 3 \
  --machine-type n2-standard-4 \
  --enable-autoscaling --min-nodes 3 --max-nodes 20 \
  --enable-ip-alias \
  --workload-pool=$(gcloud config get-value project).svc.id.goog
```

### Bước 2: Cài Đặt Các Operator Giống K3s

```bash
# Tái sử dụng script cài từ K3s — cùng Helm charts
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add linkerd https://helm.linkerd.io/stable
helm repo add jetstack https://charts.jetstack.io
helm repo update

# Cài theo thứ tự: cert-manager → Linkerd → monitoring → chaos
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.14.5/cert-manager.yaml
linkerd install --crds | kubectl apply -f -
linkerd install | kubectl apply -f -
```

### Bước 3: Triển Khai His.Hope Với Production Overlay

```bash
# Production overlay — cấu hình sẵn cho GKE
kubectl apply -k k8s/overlays/prod/

# Đợi tất cả pod sẵn sàng
kubectl wait --for=condition=ready pod -l app.kubernetes.io/part-of=his-hope -n his-hope --timeout=600s
```

### Bước 4: Khôi Phục CockroachDB

```bash
# Scale CockroachDB về 0 trên GKE trước
kubectl scale statefulset cockroachdb -n his-hope --replicas=0

# Khởi tạo cluster mới trên GKE
kubectl apply -f cockroach/cockroachdb-init.yaml

# Khôi phục từ backup
kubectl exec -n his-hope cockroachdb-0 -- \
  cockroach sql --execute="RESTORE DATABASE his_hope FROM 'gs://his-hope-backups/pre-migration'"

# Scale lên 3
kubectl scale statefulset cockroachdb -n his-hope --replicas=3
```

### Bước 5: Chuyển Đổi DNS

```bash
# Lấy IP của GKE Load Balancer
kubectl get svc istio-ingressgateway -n istio-system -o jsonpath='{.status.loadBalancer.ingress[0].ip}'

# Cập nhật DNS A record
gcloud dns record-sets transaction start --zone=hishop-zone
gcloud dns record-sets transaction add <LB-IP> --name=api.hishop.com --ttl=60 --type=A --zone=hishop-zone
gcloud dns record-sets transaction execute --zone=hishop-zone
```

### Bước 6: Xác Minh

```bash
# Tất cả service healthy
kubectl get pods -A | grep -v Running

# End-to-end test
curl https://api.hishop.com/health

# Dữ liệu SLO đang chảy
curl https://api.hishop.com/api/slo

# Prometheus targets
kubectl port-forward -n monitoring svc/prometheus-kube-prometheus-prometheus 9090:9090
# Mở http://localhost:9090/targets
```

### Bước 7: Dỡ Bỏ K3s (sau 48h theo dõi)

```bash
# Trên K3s master
sudo /usr/local/bin/k3s-uninstall.sh
# Trên từng worker
sudo /usr/local/bin/k3s-agent-uninstall.sh
```

## 5. Kế Hoạch Rollback (Nếu Có Sự Cố)

```bash
# Chuyển DNS về IP K3s node
gcloud dns record-sets transaction start --zone=hishop-zone
# ... đặt A record về IP load balancer của K3s

# Trên K3s, khởi động lại tất cả service
kubectl rollout restart deployment -n his-hope
```

## 6. Sau Khi Di Chuyển

- [ ] Theo dõi SLO error budget trong 7 ngày — không có regression
- [ ] Cập nhật monitoring guide với lệnh GKE-specific
- [ ] Cập nhật runbooks với đường dẫn GKE (`kubectl` vs `docker compose`)
- [ ] Đào tạo team về GKE console so với K3s CLI
- [ ] Lên lịch nâng cấp phiên bản GKE đầu tiên (GKE tự động nâng cấp nodes)
