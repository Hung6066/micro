# Vận hành observability bằng Ansible

## Quyết định

Nên tách thành các role độc lập, nhưng dùng một playbook điều phối duy nhất để bảo đảm thứ tự và khả năng chạy lại:

1. `linkerd_observability`: CRD, control-plane, Viz.
2. `gatekeeper_observability`: Gatekeeper và constraint templates.
3. `metrics_observability`: kube-prometheus-stack, `ServiceMonitor`, `PodMonitor`.
4. `logging_observability`: Loki và Promtail.
5. `telemetry_observability`: OpenTelemetry Collector nhận OTLP gRPC/HTTP.
6. `observability_validate`: readiness, CRD, EndpointSlice/service endpoints.

Frontend RUM dùng `otel.his-hope.local/v1/traces`. Route nằm ở
`artifacts/otel-browser-route-his-hope.yaml`, đi qua Traefik tới OTLP/HTTP
collector; Traefik bật ExternalName service có kiểm soát và NetworkPolicy chỉ
cho phép egress tới `monitoring:4318`. Backend/BFF .NET nhận cùng endpoint qua
`Otlp__Endpoint` trong overlay production.

Các role nằm dưới `ansible/enterprise-k3s/roles/`; playbook là `ansible/enterprise-k3s/playbooks/40-observability.yml`. Values không chứa secret nằm trong `artifacts/*-values.yaml`. Chứng thư Linkerd vẫn ở `D:/secure/his-hope/linkerd/` và không commit vào Git.

## Chạy production

Chạy từ Linux/WSL hoặc runner CI có Ansible và Helm (Ansible native Windows hiện lỗi `os.get_blocking`):

```bash
cd /mnt/d/AI/micro/ansible/enterprise-k3s
ansible-playbook -i inventory/production.yml playbooks/40-observability.yml \
  --vault-password-file /mnt/d/secure/his-hope/ansible-vault-password
```

Playbook dùng `upgrade --install`, vì vậy có thể chạy lại an toàn. Không chạy đồng thời hai instance trên cùng release Helm.

## Kiểm tra bắt buộc

```bash
kubectl get pods -n linkerd
kubectl get pods -n linkerd-viz
kubectl get pods -n gatekeeper-system
kubectl get pods -n monitoring
kubectl get servicemonitor,podmonitor -n monitoring
```

Prometheus, Grafana và Loki dùng PVC `local-path`; đây là lưu trữ node-local, cần backup/replication riêng trước khi coi là HA thực sự. OTLP Collector hiện nhận metrics/traces/logs; metrics được expose để Prometheus scrape, còn traces/logs được debug-exporter để kiểm thử đường ống. Cần bổ sung exporter lưu trữ tập trung (Tempo/Jaeger/Loki OTLP) trước khi cam kết retention enterprise.

Kiểm thử hiện tại đã xác nhận 78/78 Prometheus targets `up`, Promtail chạy trên
5 node, Loki `/ready` trả HTTP 200 và POST OTLP/HTTP qua VIP `172.16.102.100`
trả HTTP 200. HPA resource metrics vẫn có thể báo `unknown` nếu workload còn
pod cũ thiếu resource request cho Linkerd proxy; custom metric
`requests_per_second` cần Prometheus Adapter (chưa triển khai) trước khi dùng
cho autoscaling production.

Gatekeeper digest constraint đang deny image tag mới. Constraint chữ ký yêu cầu provider xác minh (Ratify/Cosign) được triển khai riêng; không coi `external_data()` là đã hoạt động nếu provider chưa có.
