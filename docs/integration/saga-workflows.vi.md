# Saga workflows production

Các workflow xuyên service dùng orchestration bền vững, event versioned, outbox/inbox và idempotency key. Service sở hữu dữ liệu không truy cập database của service khác.

## Luồng chuẩn

| Workflow | Orchestrator | Thứ tự chính | Compensation |
|---|---|---|---|
| Commerce fulfillment | Commerce | validate → authorize payment → reserve Manufacturing → capture → create shipment → dispatch | refund, release reservation, cancel shipment |
| Payment | Billing | authorize → capture | refund/void theo trạng thái |
| Shipment | Shipment/provider adapter | create label → dispatch → delivery webhook | cancel shipment; không tự hoàn tiền |
| Tenant provisioning | Identity | register tenant → provision data placement → seed access → activate | disable tenant, cleanup only resources created by saga |
| Content publishing | Content | publish version → invalidate cache → notify subscribers | revert publication/version; notification không rollback publication |

## Quy tắc production

- Saga id là aggregate/business id ổn định; event id chỉ dùng cho deduplication.
- Mọi command có `IdempotencyKey`, `CorrelationId`, `CausationId`, `SchemaVersion`.
- Mỗi bước ghi progress sau khi hoàn thành; retry phải an toàn; compensation phải idempotent.
- Payment capture chỉ được chạy sau authorization và reservation thành công. Không log payment token, secret, PII hoặc payload đầy đủ.
- Read-after-write của Identity giữ trên primary database. Không chuyển tenant provisioning hoặc IAM reads sang read replica.
- Provider/payment/shipment chưa có endpoint hoặc secret hợp lệ phải fail closed với trạng thái `NotConfigured`; không dùng adapter giả trong production.
- Saga table phải được migrate bởi deployment migration pipeline trước khi bật consumer; `Database.Migrate()` không thay thế migration vận hành.

Các tên workflow, step, exchange và event version được định nghĩa trong `His.Hope.Contracts.Saga.SagaWorkflowCatalog` và `SagaMessagingContract`.
