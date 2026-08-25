# 01 — R&D và quản lý công thức

## Mục tiêu

Quản lý product specification và recipe version có kiểm soát, để mỗi batch biết chính xác đã dùng định mức, routing và quality limits nào.

## Aggregate và state

| Aggregate | Thành phần | State chính |
|---|---|---|
| `ProductSpecification` | product, target moisture, packaging, shelf-life, QC spec | Draft, Approved, Retired |
| `RecipeVersion` | BOM lines, routing, parameters, yield target, effective period | Draft, Submitted, Approved, Superseded, Retired |
| `Deviation` | batch, approved parameter/ingredient substitution, reason | Open, Approved, Rejected, Closed |

`RecipeVersion` là immutable sau `Approved`. Production order giữ snapshot `recipeVersionId`, BOM, routing và quality limits; không resolve “current recipe” khi chạy batch.

## Workflow

1. R&D tạo draft từ product spec; định nghĩa input, UoM/conversion, expected yield, phase sơ chế/sấy/đóng gói và checkpoint.
2. QA review hazard/limits; Finance review cost impact khi cần.
3. Approver phê duyệt version với effective date. Planner chỉ chọn version Approved/effective.
4. Thay đổi sau release tạo version mới; bất thường tại batch tạo `Deviation`, không sửa recipe.

## Commands, events và API

| Command | Validation | Event |
|---|---|---|
| `CreateRecipeVersion` | product spec active; UoM hợp lệ | `RecipeVersionCreated` |
| `SubmitRecipeVersion` | BOM/routing/checkpoint đầy đủ | `RecipeVersionSubmitted` |
| `ApproveRecipeVersion` | approver khác author; effective date | `RecipeVersionApproved` |
| `RetireRecipeVersion` | không có order future release | `RecipeVersionRetired` |
| `CreateDeviation` | batch active, reason/impact bắt buộc | `DeviationRaised` |

API query: recipe search, recipe detail/version diff, usage history, planned-versus-actual variance.

## Quyền và tự động hóa

R&D draft/submit; QA approve quality limits; Planner read/select; Production chỉ đọc snapshot. `RecipeVersionApproved` cập nhật planner catalog; không tự tạo production order. Cảnh báo khi recipe gần expiry hoặc batch dùng deviation chưa đóng.

## Edge cases

- Mùa nguyên liệu thay đổi: tạo version hoặc deviation có expiry, không overwrite yield target.
- Substitution: phải chỉ rõ material equivalence, allergy/quality impact và approver.
- Unit mismatch: tất cả BOM quantity quy về base UoM trước khi tính variance.

## Acceptance criteria

- Không sửa được BOM/parameter của version đã Approved.
- Một production order luôn render được snapshot recipe dù bản mới đã có.
- Audit nêu author, approver, effective time, before/after và reason.
- Test: stale ETag bị reject; author không tự approve; recipe retire không ảnh hưởng batch lịch sử.
