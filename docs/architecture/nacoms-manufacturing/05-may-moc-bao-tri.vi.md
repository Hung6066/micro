# 05 — Máy móc và bảo trì

## Mục tiêu

Đo trạng thái line/máy và downtime đáng tin, lập preventive maintenance, sau đó mới pilot telemetry read-only.

## Model

`EquipmentClass`, `Equipment`, `EquipmentCapability`, `MeterReading`, `MachineStateEvent`, `DowntimeEvent`, `MaintenancePlan`, `MaintenanceWorkOrder`. States: Available, Running, Changeover, Cleaning, PlannedMaintenance, UnplannedDown, Blocked.

## Workflow

- Supervisor ghi changeover/downtime với batch/operation/reason.
- Meter hoặc OPC-UA gateway chỉ ghi telemetry read-only; adapter chuẩn hóa tag vendor thành state/meter event.
- Rule engine tạo maintenance work order theo calendar/runtime/condition; technician complete với evidence.
- API hiện hỗ trợ tạo work order preventive theo máy, chống mở trùng, hoàn tất kèm technician/evidence; khi hoàn tất sẽ cập nhật `LastMaintenanceAt`, `NextMaintenanceAt`, mở khóa máy và phát outbox `Manufacturing.MaintenanceWorkOrderCreated.v1`/`Completed.v1`.
- Planner có thể chạy `POST /api/v1/manufacturing/maintenance-work-orders/generate` để sinh các work order đến hạn từ `NextMaintenanceAt`; transaction serializable và kiểm tra work order mở giúp lần chạy lặp lại không tạo bản ghi/event trùng.
- Telemetry read-only nhận `eventId`, `observedAt`, `source`, state/meter và sequence qua `/machines/{machineId}/telemetry`; event trùng trả lại bản ghi cũ, event đến trễ vẫn lưu để audit nhưng không tự chuyển trạng thái máy hay complete operation.
- Planner nhận capacity impact từ planned/unplanned downtime, không điều khiển PLC qua ERP UI.

## KPI/contracts

`EquipmentDowntimeRecorded` gồm equipment, state, reason, start/end, batch/operation?, source. OEE chỉ tính khi có planned production time, run time, ideal rate, good count/reject count; thiếu mẫu số hiển thị `insufficient-data`.

Dashboard OEE hiện trả planned/run/good/reject facts, availability/quality nếu đủ mẫu số và giữ `OeePercent`/performance null khi chưa có `ideal_rate`; không suy diễn điểm OEE từ dữ liệu thiếu.

## Acceptance criteria

- Downtime event idempotent, không overlap không có lý do.
- Machine unavailable không được assigned batch nếu không có override approval.
- Mất OPC connection tạo alarm, không tự complete operation.
- Test telemetry duplicate/out-of-order, PM threshold và maintenance lockout.
- Integration coverage: work order mở/complete, chống trùng, cập nhật lịch kế tiếp và outbox events.
- Integration coverage: planner sinh work order đến hạn idempotent.
- Integration coverage: telemetry duplicate, out-of-order và không làm thay đổi machine state.
