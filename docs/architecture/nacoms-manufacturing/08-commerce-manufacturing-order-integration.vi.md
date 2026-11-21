# 08 — Tích hợp Commerce Order với Manufacturing Allocation

## Quyết định

Commerce không gọi trực tiếp database hoặc endpoint nội bộ của Manufacturing.
Sau khi order được chấp nhận, Commerce ghi một message outbox cùng transaction với
order. Outbox publisher phát fact `Commerce.OrderPlaced.v1`; Manufacturing nhận
fact và tạo reservation/allocation theo tenant, SKU và quantity.

## Contract hiện tại

Contract dùng chung nằm tại `His.Hope.Contracts.Commerce.CommerceOrderPlacedV1` và
bao gồm:

- `eventId`, `schemaVersion`, `occurredAt`;
- `orderId`, `tenantKey`, `buyerUserId`, `totalAmount`;
- line items: `productId`, `sku`, `quantity`, `unitPrice`;
- `correlationId` và `causationId`.

`CommerceOrderEventFactory` chuyển `OrderDto` sang contract này. Contract không
phụ thuộc RabbitMQ để có thể dùng lại cho HTTP replay, test và các transport khác.

## Idempotency và failure semantics

1. Commerce tạo `orderId` ổn định và ghi order + outbox trong một transaction.
2. Publisher có retry/backoff, không tạo event mới khi retry.
3. Manufacturing lưu receipt theo `(eventId, orderId)` hoặc khóa tương đương.
4. Event trùng chỉ trả lại kết quả allocation trước đó, không trừ ATP lần hai.
5. Nếu ATP không đủ, Manufacturing phát trạng thái allocation thất bại; Commerce
   không tự ghi thành công giả.

## Checkpoint triển khai

- [x] Shared `Commerce.OrderPlaced.v1` contract.
- [x] Factory map từ Commerce order.
- [x] Contract serialization test.
- [x] Commerce persistent order/outbox adapter và migration.
- [x] Commerce persistent store trở thành source of truth cho orders và order status.
- [x] Commerce outbox worker/RabbitMQ publisher (feature-flag `Outbox:Enabled`).
- [x] Manufacturing consumer tạo allocation idempotent (feature-flag `Consumers:CommerceOrdersEnabled`).
- [x] End-to-end RabbitMQ test: `CommerceOrderRabbitMqTests` và runtime smoke
  `scripts/config/smoke-commerce-manufacturing.ps1`.
- [x] Commerce persistent cart/profile/notification/RFQ adapters và migrations;
  API đọc/ghi các aggregate này từ PostgreSQL theo tenant/user scope.
- [x] Commerce catalog persistence và bootstrap migration; catalog được hydrate từ
  PostgreSQL trước khi phục vụ products/orders.

Order, order status, cart, profile, notification, RFQ và catalog hiện đọc/ghi từ
PostgreSQL; `CommerceStore` chỉ giữ bản hydrate trong process để phục vụ tính giá và
validation nhanh.
Worker và consumer đã được bật trong Docker runtime, và smoke script chứng minh
order event đi qua RabbitMQ, allocation được tạo, rồi duplicate replay không tạo
reservation thứ hai.
