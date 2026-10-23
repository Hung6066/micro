# 06 — Chất lượng, hao hụt và giá thành

## Mục tiêu

Tách facts chất lượng, mass balance, loss và cost để thấy rõ hao hụt ở công đoạn nào và không “làm đẹp” KPI.

## Model

`QualitySpecification`, `QualityInspection`, `QualityDisposition`, `LossRecord`, `LossReasonCode`, `BatchCost`, `CostAllocationPolicy`. QC inspection bám `Lot` hoặc `Batch/Operation`; result không overwrite, correction là record mới liên kết record cũ.

## Công thức

```text
Loss qty = measured input - good output - rework output - approved by-product
Operation loss % = loss qty / measured input × 100
Batch yield % = released FG qty / approved raw input × 100
Recipe variance % = (actual consumption - planned consumption) / planned consumption × 100
```

Tất cả quantity phải quy về base UoM và có moisture/rounding policy. Reject, rework, natural process loss và inventory variance là reason group khác nhau.

## Controls và automation

QC quyết định Release/Hold/Reject/Rework. Loss vượt recipe threshold tạo `LossThresholdExceeded`, yêu cầu supervisor reason/approval; không tự ghi adjustment tồn. Cost projection nhận actual issued materials, labour/machine time, packaging, QC/rework và overhead policy version. Finance Close period khóa cost; correction post-close là adjustment event.

### Đã triển khai trong Manufacturing Service

- `POST /api/v1/manufacturing/production-batches/{batchId}/operations/{operationId}/loss-review` ghi quyết định `Approved` hoặc `Rejected`, reviewer, ghi chú và thời điểm vào `manufacturing_loss_reviews`.
- `POST .../complete` trả `422 loss_review_required` nếu một operation có yield thấp hơn target recipe mà chưa có review được approve; không tạo output lot, issue/produce ledger hoặc event hoàn tất trong lần bị chặn.
- Review và thay đổi trạng thái đều phát outbox event (`Manufacturing.LossThresholdReviewed.v1` / `Manufacturing.ProductionBatchCompleted.v1`) để consumer/read model xử lý idempotent.
- Executive exceptions chỉ bỏ cảnh báo loss sau review `Approved`; review `Rejected` vẫn giữ cảnh báo để supervisor xử lý tiếp.

## Acceptance criteria

- Batch không Close nếu mass balance chưa giải thích được.
- Hold FG gỡ ATP ngay nhưng vẫn giữ genealogy/cost history.
- Loss dashboard drill down đến measurement nguồn và approver.
- Test rework loop, moisture conversion, threshold override, correction after period close.
