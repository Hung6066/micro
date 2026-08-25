# 03 — Kho, tồn và truy xuất

## Mục tiêu

Tạo single source of truth cho tồn theo lot/location và truy xuất nhanh khi QA hold/recall.

## Ledger model

`InventoryTransaction` là append-only: `Receipt`, `Move`, `Reserve`, `Unreserve`, `Issue`, `Produce`, `Adjust`, `Ship`, `Return`, `Hold`, `Release`. `StockBalance` là projection theo `facility/location/lot/stockStatus`; adjustment bắt buộc reason, approver và evidence.

## Workflow

1. Nhận/produce tạo stock theo lot và disposition.
2. Reservation giữ lượng cho order/batch; FEFO chỉ chọn lot Released, unexpired.
3. Issue/produce tạo `LotTransformation` ở operation; transfer không tạo lot mới.
4. Hold loại lot khỏi ATP/reservation; recall query duyệt genealogy ngược/xuôi.

## API/event

Commands: `MoveStock`, `ReserveLot`, `IssueLotToBatch`, `RecordCycleCount`, `ApproveInventoryAdjustment`, `PlaceLotOnHold`.

Events: `InventoryReserved`, `InventoryIssued`, `LotMoved`, `InventoryAdjusted`, `LotHeld`, `LotGenealogyLinked`.

## Recall contract

Query `GET /lots/{id}/genealogy?direction=upstream|downstream` trả lot, quantity linked, batch, operation, location, disposition và related shipment/reservation. Không trả số tồn tổng không có lot.

## Acceptance criteria

- Không thể issue nhiều hơn available released balance sau reservation.
- Affected FG lots và customer allocation được tìm từ raw lot trong recall drill.
- Replay cùng event không nhân đôi balance; ledger tổng hợp về đúng projection.
