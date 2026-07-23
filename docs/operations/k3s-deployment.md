# Hướng dẫn Triển khai His.Hope trên K3s

> **Tài liệu:** OPS-DEPLOY-002
> **Version:** 1.0
> **Audience:** SRE, DevOps, On-prem Deployer
> **Cập nhật:** 2026-07-23

Tài liệu này hướng dẫn triển khai toàn bộ hệ thống His.Hope EMR trên cụm K3s (on-premise hoặc VM cloud). Phù hợp cho môi trường dev, staging, và production quy mô vừa.

---

## 1. Yêu cầu Tiên quyết (Prerequisites)

- **1+ server** chạy Ubuntu 22.04 LTS hoặc Debian 12
- **Tối thiểu:** 4 CPU, 8GB RAM cho single-node; khuyến nghị 8 CPU, 16GB cho multi-node
- **SSH access** với quyền sudo
- **Không cần cài Docker** — K3s tích hợp containerd built-in
- **Cổng mở trên firewall:**

| Cổng | Mục đích |
|------|----------|
| 6443 | K3s API server |
| 80/443 | HTTP/HTTPS Ingress |
| 30000–32767 | NodePort (fallback khi chưa cấu hình Ingress) |
| 8472 | Flannel VXLAN (multi-node) |

**CLI tools cần cài trên workstation điều khiển:**
- `kubectl` (1.28+)
- `helm` (3.x)
- `linkerd` CLI (2.14+)
- `jq`

```bash
# Cài kubectl
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
sudo install -o root -g root -m 0755 kubectl /usr/local/bin/kubectl

# Cài Helm
curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash

# Cài Linkerd CLI
curl --proto '=https' --tlsv1.2 -sSfL https://run.linkerd.io/install | sh
export PATH=$PATH:$HOME/.linkerd2/bin
```

---

## 2. Cài đặt K3s (Single Node)

```bash
curl -sfL https://get.k3s.io | sh -s - \
  --disable traefik \
  --write-kubeconfig-mode 644
```

K3s tắt Traefik built-in vì His.Hope sử dụng Ingress controller riêng và Linkerd service mesh.

```bash
# Đợi ~30 giây để API server sẵn sàng
sleep 30
kubectl get nodes
# Output mong đợi: STATUS = Ready

# Lấy kubeconfig về workstation
mkdir -p ~/.kube
sudo cat /etc/rancher/k3s/k3s.yaml > ~/.kube/config
sudo chown $(id -u):$(id -g) ~/.kube/config

# Kiểm tra cluster
kubectl cluster-info
kubectl get pods -A
```

### 2.1 Cài đặt K3s Multi-Node (Master + Worker)

```bash
# === Trên master node ===
curl -sfL https://get.k3s.io | sh -s - \
  --disable traefik \
  --write-kubeconfig-mode 644 \
  --node-taint CriticalAddonsOnly=true:NoExecute

# Lấy token join
sudo cat /var/lib/rancher/k3s/server/node-token
# Lưu token này, dùng cho worker node

# === Trên worker node ===
export K3S_URL=https://<MASTER_IP>:6443
export K3S_TOKEN=<TOKEN>

curl -sfL https://get.k3s.io | sh -

# === Xác nhận trên master ===
kubectl get nodes
# Output: 2+ nodes, tất cả STATUS = Ready
```

---

## 3. Cài đặt Operators & Tools Hạ tầng

Thực hiện theo đúng thứ tự sau (mỗi layer phụ thuộc layer trước):

### 3.1 cert-manager (Chứng chỉ TLS)

```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.14.5/cert-manager.yaml

# Đợi cert-manager pods ready
kubectl wait pod -l app.kubernetes.io/instance=cert-manager -n cert-manager --for=condition=Ready --timeout=120s
```

### 3.2 Linkerd Service Mesh (mTLS)

```bash
# Pre-flight check
linkerd check --pre

# Cài Linkerd CRDs
linkerd install --crds | kubectl apply -f -

# Cài Linkerd control plane
linkerd install | kubectl apply -f -

# Kiểm tra
linkerd check
# Output mong đợi: "Status check results are √"

# Cài Linkerd Viz (dashboard + metrics)
linkerd viz install | kubectl apply -f -
linkerd viz check

# Cài Linkerd Jaeger (distributed tracing — tùy chọn)
linkerd jaeger install | kubectl apply -f -
```

### 3.3 Prometheus Stack (Giám sát)

```bash
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install prometheus prometheus-community/kube-prometheus-stack \
  -n monitoring --create-namespace \
  --set prometheus.prometheusSpec.serviceMonitorSelectorNilUsesHelmValues=false

# Đợi Prometheus + Grafana ready
kubectl wait pod -l app.kubernetes.io/name=grafana -n monitoring --for=condition=Ready --timeout=300s
```

### 3.4 Chaos Mesh (Resilience Testing — tùy chọn)

```bash
helm repo add chaos-mesh https://charts.chaos-mesh.org
helm repo update

helm install chaos-mesh chaos-mesh/chaos-mesh \
  -n chaos-engineering --create-namespace \
  --set chaosDaemon.runtime=containerd \
  --set chaosDaemon.socketPath=/run/k3s/containerd/containerd.sock

kubectl wait pod -l app.kubernetes.io/instance=chaos-mesh -n chaos-engineering --for=condition=Ready --timeout=300s
```

---

## 4. Triển khai His.Hope

### 4.1 Chuẩn bị Manifests

```bash
# Clone repository (nếu chưa có)
git clone https://github.com/your-org/his-hope.git /opt/his-hope
cd /opt/his-hope

# Tạo namespace + Linkerd injection annotation
kubectl apply -f k8s/base/namespace.yaml

# Xác nhận namespace
kubectl get ns | Select-String "his-hope|linkerd|monitoring|chaos-engineering|vault"
```

### 4.2 Deploy Application

```bash
# Triển khai toàn bộ overlay dev (single replica, resource thấp)
kubectl apply -k k8s/overlays/dev/

# Đợi tất cả pods sẵn sàng (tối đa 5 phút)
kubectl wait --for=condition=ready pod \
  -l app.kubernetes.io/part-of=his-hope \
  -n his-hope-dev --timeout=300s

# Kiểm tra trạng thái
kubectl get pods -n his-hope-dev -o wide
kubectl get svc -n his-hope-dev
```

> **Lưu ý:** Overlay `dev` dùng namespace `his-hope-dev` và prefix `his-hope-` trong tên resource. Để triển khai production, dùng `k8s/overlays/prod/`.

---

## 5. Truy cập Dashboard

### 5.1 Port-forward (nhanh, dùng để test)

```bash
# Dashboard frontend
kubectl port-forward -n his-hope-dev svc/his-hope-dashboard-app 8082:80

# Truy cập: http://localhost:8082

# System Dashboard BFF (API SLO, health cluster)
kubectl port-forward -n his-hope-dev svc/his-hope-systemdashboard-bff 5700:5700
```

### 5.2 NodePort (truy cập từ xa)

```bash
# Chuyển Service sang NodePort
kubectl patch svc his-hope-dashboard-app -n his-hope-dev \
  -p '{"spec":{"type":"NodePort"}}'

# Lấy cổng NodePort được gán
$NODE_PORT = kubectl get svc his-hope-dashboard-app -n his-hope-dev \
  -o jsonpath='{.spec.ports[0].nodePort}'
Write-Host "NodePort: $NODE_PORT"

# Truy cập: http://<node-ip>:$NODE_PORT
```

---

## 6. Kiểm tra Tổng thể (Verification)

### 6.1 Tất cả Pods Running

```bash
kubectl get pods -A
# Tất cả STATUS = Running, READY = 1/1 hoặc 2/2 (nếu đã inject Linkerd)
```

### 6.2 Health Check BFF

```bash
kubectl exec -n his-hope-dev deploy/his-hope-systemdashboard-bff -- \
  wget -qO- http://localhost:5700/health
# Output: Healthy
```

### 6.3 Prometheus Scrape Targets

```bash
kubectl port-forward -n monitoring svc/prometheus-kube-prometheus-prometheus 9090:9090 &
# Mở http://localhost:9090/targets → tất cả targets phải UP (màu xanh)
```

### 6.4 Grafana Dashboards

```bash
# Lấy mật khẩu admin Grafana
$GF_PASS = kubectl get secret prometheus-grafana -n monitoring \
  -o jsonpath='{.data.admin-password}' | %{[System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_))}
Write-Host "Grafana admin password: $GF_PASS"

kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80 &
# Mở http://localhost:3000 → login admin/$GF_PASS
```

### 6.5 Linkerd Dashboard

```bash
linkerd viz dashboard &
# Mở http://localhost:50750 → kiểm tra tất cả deployments meshed
```

---

## 7. Scaling Services

### 7.1 Auto-scaling với KEDA (Event-driven)

```bash
# Deploy KEDA
helm repo add kedacore https://kedacore.github.io/charts
helm repo update
helm install keda kedacore/keda -n keda --create-namespace

# Apply KEDA ScaledObjects
kubectl apply -f k8s/autoscaling/keda-scaledobjects.yaml
```

### 7.2 Auto-scaling với HPA (CPU/Memory)

```bash
# Apply HorizontalPodAutoscaler definitions
kubectl apply -f k8s/autoscaling/

# Kiểm tra HPA status
kubectl get hpa -n his-hope-dev
```

### 7.3 Scale Thủ công (Testing)

```bash
# Scale một service cụ thể
kubectl scale deployment his-hope-patient-service -n his-hope-dev --replicas=5

# Theo dõi quá trình scale
kubectl get pods -n his-hope-dev -l app.kubernetes.io/name=patient-service -w
```

---

## 8. Thêm Worker Node vào Cụm

```bash
# === Trên master: lấy token join ===
sudo cat /var/lib/rancher/k3s/server/node-token

# === Trên worker mới: join cluster ===
curl -sfL https://get.k3s.io | \
  K3S_URL=https://<MASTER_IP>:6443 \
  K3S_TOKEN=<TOKEN> \
  sh -

# === Xác nhận trên master ===
kubectl get nodes
# Output: hiển thị thêm node mới, STATUS = Ready
```

---

## 9. Khắc phục Sự cố (Troubleshooting)

| Triệu chứng | Cần kiểm tra | Cách sửa |
|-------------|-------------|----------|
| **Pod stuck Pending** | `kubectl describe pod <pod>` — xem Events | Thiếu tài nguyên CPU/RAM, PVC không bound, node không đủ capacity |
| **CrashLoopBackOff** | `kubectl logs <pod> -n his-hope-dev --tail=100` | Sai connection string DB, file cấu hình thiếu, secret chưa tạo |
| **Service không reachable** | `kubectl get endpoints <svc> -n his-hope-dev` | Label selector không khớp với pod labels |
| **Prometheus không scrape được metrics** | Kiểm tra ServiceMonitor labels | Label `release: prometheus` phải có trên ServiceMonitor |
| **CockroachDB init fail** | `kubectl logs cockroachdb-0 -n his-hope` | Lỗi DNS resolution, kiểm tra init job đã chạy chưa |
| **Linkerd proxy không inject** | `kubectl get ns his-hope-dev -o yaml` | Annotation `linkerd.io/inject: enabled` phải có trên namespace |
| **cert-manager không cấp chứng chỉ** | `kubectl get certificaterequests -A` | Kiểm tra Issuer/ClusterIssuer đã sẵn sàng, DNS-01 challenge |
| **Không đủ disk space** | `df -h` trên node | K3s image store tại `/var/lib/rancher/k3s/agent/containerd` — dùng `k3s crictl rmi --prune` |

---

## 10. Tham khảo

- [K3s Architecture](https://docs.k3s.io/architecture)
- [Linkerd trên K3s](https://linkerd.io/2.14/tasks/installing-linkerd-on-k3s/)
- [cert-manager Installation](https://cert-manager.io/docs/installation/)
- [Deployment Guide đầy đủ](deployment-guide.md) — Hướng dẫn production chi tiết (ArgoCD, Vault, Canary)
- [Monitoring Guide](monitoring-guide.md) — Cấu hình Prometheus, Grafana, Alerts
- [Disaster Recovery](disaster-recovery.md) — Backup & Restore CockroachDB
