# Nacoms Manufacturing — bộ tài liệu triển khai

## Mục đích

Bộ tài liệu chuyển mục tiêu vận hành Nacoms thành bounded contexts, data contracts và acceptance gates. Tài liệu tổng quan hiện hữu: [Operating model](../nacoms-manufacturing-operating-model-implementation.vi.md). Bằng chứng chuẩn ngành: [research](../../research/2026-08-25-food-manufacturing-traceability-automation-research.vi.md).

## Thứ tự đọc và triển khai

| # | Tài liệu | Phụ thuộc | Milestone implementation |
|---|---|---|---|
| 01 | [R&D và công thức](01-rd-cong-thuc.vi.md) | Master data | Recipe version được phê duyệt và snapshot |
| 02 | [Thu mua và nguyên liệu](02-thu-mua-nguyen-lieu.vi.md) | Supplier, UoM | Receipt/QC tạo raw-material lot |
| 03 | [Kho và truy xuất](03-kho-truy-xuat.vi.md) | Lot, location | Ledger + genealogy + recall drill |
| 04 | [Sản xuất](04-san-xuat-so-che-say-dong-goi.vi.md) | 01–03 | Batch/operation/WIP được ghi nhận |
| 05 | [Máy móc và bảo trì](05-may-moc-bao-tri.vi.md) | 04 | Downtime/OEE/PM; device read-only pilot |
| 06 | [Chất lượng, hao hụt, giá thành](06-chat-luong-hao-hut-gia-thanh.vi.md) | 01–05 | Mass balance và batch cost đối soát |
| 07 | [Sales, CEO và điều hành](07-sales-ceo-dieu-hanh.vi.md) | 02–06 | ATP, margin, dashboard/alerts |

## Quy ước contract

Command làm thay đổi state và dùng optimistic concurrency. Event là facts đã xảy ra, có `eventId`, `schemaVersion`, `occurredAt`, `correlationId`, `facilityId`; producer dùng Outbox, consumer Inbox/idempotency. Query chỉ đọc projection; không được tái sử dụng transaction UI cho dashboard CEO.

## Pilot đề nghị

Pilot một family **xoài sấy** tại một facility/line, với một flow từ inbound receipt đến FG release. Chỉ mở rộng sau khi recall drill, mass-balance và người dùng xưởng xác nhận được.
