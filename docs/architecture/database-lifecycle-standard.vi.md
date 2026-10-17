# Chuẩn database dùng chung His.Hope

## Hợp đồng vật lý

Mọi EF write model dùng `HisHopeDataConventions.Apply(...)`. Tên bảng và tên
cột domain được chuẩn hóa `snake_case`, ví dụ `production_manager`.

Mọi bảng có metadata lifecycle:

- `created_at`, `created_by`
- `updated_at`, `updated_by`
- `is_deleted`, `deleted_at`, `deleted_by`

Các cột được khai báo bằng shadow property nên domain service không phải phụ
thuộc ngược vào Infrastructure.

## Soft delete và audit

Context khai báo rõ aggregate nào được soft delete. `SoftDeleteInterceptor`
stamp audit metadata và chuyển `DELETE` thành tombstone cho các aggregate đó.
Audit log, outbox, event receipt, token và lịch sử bất biến không được soft
delete vì chúng cần append-only hoặc cần cơ chế expiry/delivery vật lý; tuy
nhiên tên vật lý của chúng vẫn dùng cùng chuẩn `snake_case`.

## Rollout

Migration phải chạy theo expand/contract. Không drop cột legacy trong cùng
release với việc đổi mapping; cần backfill, dual-read/dual-write và chỉ drop
sau khi mọi worker cũ đã được thay thế. Phần `Up` của migration lifecycle không
được có `DropColumn`; `Down` chỉ là rollback không hỗ trợ tự động. Migration có
rename legacy phải được review theo database cụ thể và chạy trong release có
kiểm soát.

## Kết quả audit runtime ngày 2026-08-27

- `identitydb`: đã apply `20260827072852_StandardizeDataLifecycle`; tất cả bảng nghiệp vụ có đủ 7 lifecycle columns và không còn uppercase column.
- Tám database nghiệp vụ còn lại đã được reconcile lifecycle columns bằng SQL idempotent trên database local đang chạy; audit sau đó ghi nhận `0` bảng thiếu lifecycle columns.
- Physical business identifier naming đã được reconcile trên local PostgreSQL: business tables/columns đều `snake_case`; outbox/event-receipt dùng cùng naming contract, chỉ migration-history là system exception.
- Các migration physical identifier dùng converter PascalCase→snake_case, collision backfill bằng `COALESCE(new, legacy)`, sau đó drop legacy column; chỉ loại trừ bảng migration history hệ thống.
- Đã rebuild/recreate các image bị ảnh hưởng và xác nhận các container Manufacturing, Content, Lab, Pharmacy, Patient, Appointment, Clinical, Billing và Identity đều healthy; log sau reconcile không còn lỗi relation/column schema.
