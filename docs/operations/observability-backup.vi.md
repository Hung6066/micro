# Observability backup production

## Kết luận

Có backup observability, nhưng tách dữ liệu cấu hình và dữ liệu telemetry:

- **Bắt buộc:** Grafana dashboards/datasources, Prometheus rules/config, Alertmanager routing/silences, recording rules, ServiceMonitor/PodMonitor và các ConfigMap.
- **Tùy mục đích:** Prometheus time-series, Loki logs và Jaeger traces. Nếu dùng cho audit/compliance hoặc điều tra sự cố dài hạn thì phải backup off-site; nếu chỉ troubleshooting ngắn hạn thì đặt retention ngắn và không coi là hệ thống dữ liệu bền vững.

## Hiện trạng K3s đã kiểm tra

- Context hiện tại: `k3d-his-hope`.
- 3 node K3s: 1 server + 2 agent, tất cả `Ready`, phiên bản `v1.35.5+k3s1`.
- Namespace `monitoring` đang chạy Prometheus, Grafana, Alertmanager, Loki, Jaeger, OTel Collector và Promtail.
- PVC observability: Grafana 2Gi, Jaeger 5Gi, Loki 10Gi, Prometheus 10Gi.
- Tất cả PVC đang dùng `local-path`; đây không phải DR storage.

## Cách backup

1. Chạy `scripts/export-observability-config-to-azure.sh` định kỳ để lưu cấu hình không chứa Secret.
2. Lưu dashboard/rules/config trong Git và Azure Blob.
3. Dùng Velero + CSI/filesystem backup cho 4 PVC observability sau khi bỏ phụ thuộc `local-path`.
4. Restore thử vào namespace cô lập; kiểm tra Grafana login/dashboard, Prometheus query, Loki query và Jaeger trace query.

Không export Kubernetes Secret plaintext. Grafana admin/OIDC credentials và TLS material phải phục hồi từ Vault/secret manager.
