# 04 — Sản xuất: sơ chế, sấy và đóng gói

## Mục tiêu

Biến recipe snapshot thành batch thực tế, đo input/output theo công đoạn và tạo WIP/finished-good lot truy xuất được.

## Model và workflow

`ProductionOrder` (Planned, Released, InProgress, Completed, Cancelled) chứa target và snapshot. `ProductionBatch` (Created, Started, Paused, Completed, Closed) thực hiện order. `OperationExecution` cho sơ chế/sấy/đóng gói lưu station, operator, time, input/output/loss và QC status.

1. Planner release order; supervisor tạo/start batch và chọn equipment/operator.
2. Warehouse issue reserved raw lots; batch ghi cân thực tế.
3. Hoàn thành operation tạo WIP lot hoặc FG lot, transformation links và QC task.
4. QA release FG; batch chỉ Close khi mass balance, QC và deviation được giải quyết.

## Commands/events

| Command | Event | Invariant |
|---|---|---|
| `ReleaseProductionOrder` | `ProductionOrderReleased` | recipe snapshot immutable |
| `StartBatch` | `ProductionBatchStarted` | equipment available, input allowed |
| `RecordOperationMeasurement` | `OperationMeasurementRecorded` | quantity/UoM/time/operator |
| `CompleteOperation` | `OperationCompleted` | mass balance pending or justified |
| `CompleteBatch` | `ProductionBatchCompleted` | routing/QC/hold policy pass |

## Automation và UI

Tablet xưởng tối ưu barcode scan + cân thực tế + offline queue; không yêu cầu operator nhập giá thành. Event `OperationCompleted` tạo QC task/cost WIP/stock projection. Dashboard supervisor hiển thị planned vs actual, current operation, blockers và overdue QC.

## Acceptance criteria

- Không complete batch khi một operation bắt buộc chưa đo output.
- One raw lot can feed many batches; one batch can produce many FG lots.
- Offline retry không tạo duplicate measurement/transformation.
- Test pause/resume, partial batch, rework, changeover và cancelled order.
