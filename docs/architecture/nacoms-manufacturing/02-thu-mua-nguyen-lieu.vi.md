# 02 — Thu mua và nguyên liệu

## Mục tiêu

Đảm bảo nguyên liệu có nguồn gốc, chứng từ, QC disposition và landed cost trước khi xưởng được phép tiêu hao.

## Model

`Supplier` → `RFQ/Quotation` → `PurchaseOrder` → `InboundReceipt` → `RawMaterialLot` → `QualityInspection` → `Disposition`. Một receipt có nhiều lot/pallet; một supplier lot có thể bị split nhưng không được mất parent reference.

| Command | Event | Rule chính |
|---|---|---|
| `CreatePurchaseOrder` | `PurchaseOrderCreated` | supplier/material active, price/currency/lead time |
| `ReceiveInboundLot` | `RawMaterialLotReceived` | lot code unique per facility, quantity/weight source bắt buộc |
| `RecordInboundInspection` | `QualityInspectionRecorded` | sample/result/spec version |
| `SetLotDisposition` | `RawMaterialLotReleased/Held/Rejected` | QA role, reason khi non-release |

## Workflow và automation

Receipt tạo lot `Quarantine` và inventory ledger receipt transaction. QA release mới tạo available supply. Demand từ production/forecast tạo purchase suggestion; Procurement duyệt tạo PO, hệ thống không tự gửi PO cho vendor. Lot sắp hết hạn, QC overdue và lead-time risk tạo task/alert.

## Contracts

`RawMaterialLotReceived` phải chứa `lotId`, `materialId`, `supplierId`, `supplierLotCode`, `receiptId`, `quantity`, `uom`, `receivedAt`, `expiryDate?`, `facilityId`. Giá/landed cost là event riêng để không chặn receipt.

## Acceptance criteria

- Warehouse không reserve/issue lot Quarantine/Hold/Rejected.
- Supplier lot, document và QC history truy ngược được từ raw lot.
- Partial receipt/partial rejection không làm sai PO remaining quantity.
- Test duplicate scan, over-receipt policy, split lot và release idempotency.
