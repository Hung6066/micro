---
id: devops-decision-01
type: decision
domain: devops
tags: [linkerd, service-mesh, istio, kubernetes]
severity: info
agent: @devops
author: @architect
date: 2026-07-17
related: [adr-003]
---

# Chọn Linkerd thay vì Istio làm service mesh

## Quyết định
Dùng Linkerd làm service mesh cho toàn bộ His.Hope cluster.

## Lý do
- Linkerd nhẹ hơn 10x về resource (50MB vs 500MB RAM mỗi pod sidecar)
- Cấu hình đơn giản — không cần CRDs phức tạp như Istio (VirtualService, DestinationRule, Gateway...)
- mTLS tự động giữa tất cả pod mà không cần cấu hình thủ công
- Phù hợp với team size hiện tại (không cần đội ngũ chuyên trách service mesh)

## Trade-off đã cân nhắc
| Tiêu chí | Linkerd | Istio |
|---|---|---|
| Resource footprint | ~50MB/pod | ~500MB/pod |
| Cấu hình | Tự động, ít can thiệp | CRDs phức tạp |
| mTLS | Tự động mesh-wide | Cần PeerAuthentication policy |
| Multi-cluster | Hạn chế (multi-cluster gateways) | Mạnh (Istio multi-cluster) |
| Ecosystem | Nhỏ hơn | CNCF graduated, ecosystem lớn |
| Observability | Tích hợp Prometheus + Grafana | Kiali, Jaeger, Grafana |

## Khi nào xem xét lại
- Khi cần multi-cluster mesh federation phức tạp (>3 clusters)
- Khi team có >5 SRE chuyên trách
- Khi cần advanced traffic routing (A/B testing, canary với header-based routing)

## Tham khảo
- `docs/adr/003-linkerd-over-istio.md`
- `docs/linkerd-guide.md`
