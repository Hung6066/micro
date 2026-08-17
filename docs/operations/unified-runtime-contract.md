# His.Hope unified runtime contract

`config/runtime-contract.v1.json` là hợp đồng chuẩn cho các giá trị mà workload
được phép đọc ở Docker Compose, VM/systemd và Kubernetes/K3s. Hợp đồng chỉ chứa
endpoint ứng dụng, data plane và observability mà application cần; các service
nền tảng như Vault, Traefik, Loki và Alertmanager được khai báo riêng trong
`config/platform-contract.v1.json`.

## Kiểm tra trước khi deploy

```powershell
# Kiểm tra schema, secret provider và production safety
pwsh -NoProfile -File scripts/config/validate-runtime-contract.ps1 `
  -EnvironmentFile config/environments/development.env.example `
  -Runtime docker -Strict

# Kiểm tra Compose service/port references và rendered config
pwsh -NoProfile -File scripts/config/validate-compose-stack.ps1 `
  -ComposeFile docker/docker-compose.yml `
  -EnvironmentFile config/environments/development.env.example

# Kiểm tra K3s sau khi chọn đúng environment file của overlay
pwsh -NoProfile -File scripts/config/validate-runtime-references.ps1 `
  -EnvironmentFile config/environments/production.env.example `
  -Runtime kubernetes `
  -Kustomization k8s/overlays/prod/kustomization.yaml
```

Không dùng `development.env.example` để kiểm tra K3s hoặc production: file đó
chứa hostname Docker. Mỗi runtime phải render từ environment file riêng của nó.

## Observability endpoint chuẩn

Workload chỉ tham chiếu các key logic sau:

| Key | Compose | VM | K3s |
|---|---|---|---|
| `OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT` | `otel-collector:4317` | `otel.internal...:4317` | `otel-collector.his-hope.svc.cluster.local:4317` |
| `OBSERVABILITY_PROMETHEUS_URL` | `prometheus:9090` | `prometheus.internal...:9090` | `prometheus.monitoring.svc.cluster.local:9090` |
| `OBSERVABILITY_LOKI_URL` | `loki:3100` | `loki.internal...:3100` | `loki.monitoring.svc.cluster.local:3100` |
| `OBSERVABILITY_JAEGER_URL` | `jaeger:16686` | `jaeger.internal...:16686` | `jaeger.monitoring.svc.cluster.local:16686` |
| `OBSERVABILITY_ALERTMANAGER_URL` | `alertmanager:9093` | `alertmanager.internal...:9093` | `alertmanager.monitoring.svc.cluster.local:9093` |

Dashboard BFF dùng cùng logical keys, không hard-code namespace K3s trong
PromQL. Compose có Loki filesystem volume; K3s dùng storage của monitoring
stack.

## Secret injection theo môi trường

- Compose local: file/secret riêng dưới `docker/config` hoặc secret provider
  local, không commit giá trị thật.
- VM/systemd: render env không chứa secret value; secret được cấp bởi
  `/etc/his-hope/secrets/<service>/` với mode `0640` và service account riêng.
- K3s: dùng Vault CSI/SPIRE workload identity; không copy secret production từ
  Compose/VM sang ConfigMap hoặc Git.

## Smoke test

```powershell
# Observability subset của Compose
pwsh -NoProfile -File scripts/config/smoke-compose-observability.ps1

# VM adapter trên WSL2 có systemd (chỉ khi WSL instance hoạt động)
pwsh -NoProfile -File scripts/config/smoke-wsl-systemd.ps1
```

Smoke test WSL2 chỉ chứng minh adapter systemd trên Linux local. Production VM
vẫn cần chạy lại cùng unit trên VM Linux thật và lưu evidence riêng.
