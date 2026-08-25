# 05 — Máy móc và bảo trì

## Mục tiêu

Đo trạng thái line/máy và downtime đáng tin, lập preventive maintenance, sau đó mới pilot telemetry read-only.

## Model

`EquipmentClass`, `Equipment`, `EquipmentCapability`, `MeterReading`, `MachineStateEvent`, `DowntimeEvent`, `MaintenancePlan`, `MaintenanceWorkOrder`. States: Available, Running, Changeover, Cleaning, PlannedMaintenance, UnplannedDown, Blocked.

## Workflow

- Supervisor ghi changeover/downtime với batch/operation/reason.
- Meter hoặc OPC-UA gateway chỉ ghi telemetry read-only; adapter chuẩn hóa tag vendor thành state/meter event.
- Rule engine tạo maintenance work order theo calendar/runtime/condition; technician complete với evidence.
- Planner nhận capacity impact từ planned/unplanned downtime, không điều khiển PLC qua ERP UI.

## KPI/contracts

`EquipmentDowntimeRecorded` gồm equipment, state, reason, start/end, batch/operation?, source. OEE chỉ tính khi có planned production time, run time, ideal rate, good count/reject count; thiếu mẫu số hiển thị `insufficient-data`.

## Acceptance criteria

- Downtime event idempotent, không overlap không có lý do.
- Machine unavailable không được assigned batch nếu không có override approval.
- Mất OPC connection tạo alarm, không tự complete operation.
- Test telemetry duplicate/out-of-order, PM threshold và maintenance lockout.
