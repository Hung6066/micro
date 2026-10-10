# 07 — Sales, CEO và điều hành

## Mục tiêu

Biến facts vận hành đã xác nhận thành quyết định bán hàng và điều hành; không để Sales/CEO ghi trực tiếp vào transaction xưởng.

## Read models

| Persona | Projection | Quyết định hỗ trợ |
|---|---|---|
| Sales | ATP theo SKU/lot/expiry, reservation, allocation risk | hứa hàng, phân bổ, forecast |
| Planner | demand-supply, capacity, MRP suggestion, WIP age | release/re-plan order |
| CEO | yield/loss Pareto, OEE completeness, stock cover, margin, exception | ưu tiên/cảnh báo/approval |
| QA/Recall | affected lot, stock, shipment/customer allocation | hold/recall scope |

## ATP và forecast contract

`ATP = released FG balance - active reservations - blocked allocation`, tính theo facility, lot, expiry và promise date. `FinishedGoodsLotHeld` phải thu hồi ATP/reservation projection; Sales thấy exception và không thể promise quantity âm. Forecast là versioned input, event `SalesForecastChanged` kích hoạt MRP suggestion, không tự tạo PO/lệnh sản xuất.

## Alerts và quyền

Alert: hold ảnh hưởng đơn, expiry risk, yield below target, downtime prolonged, raw shortage, margin erosion. CEO xem drill-down; chỉ approve exception theo delegation. Sales không release quality lot, change batch cost hoặc override stock.

## Acceptance criteria

- ATP query truy ngược được lot/disposition/reservation event.
- CEO KPI có timestamp freshness và link đến source batch/lot; thiếu data phải nêu thiếu.
- Forecast change replay không nhân đôi MRP suggestion.
- Test lot hold after allocation, expiry FEFO, stale projection và role denial.

### Sales forecast đã triển khai

`SalesForecast` là input versioned theo tenant, SKU và kỳ bán hàng. Mỗi phiên bản được ghi unique theo `(tenant, SKU, period, version)` và phát event `Manufacturing.SalesForecastChanged.v1`; replay cùng version bị từ chối để không nhân đôi tác động Planner. Endpoint forecast-material-requirements chọn recipe Approved/Active mới nhất, tính nhu cầu nguyên liệu, tồn Released, reservation và shortage; forecast không tự tạo PO hoặc lệnh sản xuất.
