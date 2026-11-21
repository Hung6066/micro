# Nghiên cứu chuẩn tham chiếu: truy xuất, an toàn thực phẩm và tự động hóa sản xuất Nacoms

Ngày: 2026-08-25  
Phạm vi: bằng chứng chuẩn gốc cho các module nghiên cứu/công thức, thu mua-nguyên liệu, sản xuất, máy móc, thành phẩm-kho, hao hụt-giá thành và điều hành. Đây là tài liệu định hướng triển khai, không xác nhận chứng nhận ISO hay tuân thủ pháp lý của Nacoms.

## Kết luận thực hành

Nền tảng phải coi **lô vật lý và sự kiện biến đổi** là nguồn sự thật chung, không coi phiếu kho hay lệnh sản xuất là dữ liệu độc lập. Mỗi lần nhận hàng, QC, cấp phát, sơ chế, sấy, đóng gói, chuyển kho và xuất bán tạo một sự kiện bất biến có thời điểm, địa điểm, người/máy thực hiện, lượng đo thực tế và liên kết input-output theo lô. Từ đồ thị này hệ thống mới tự động tính yield/hao hụt, truy xuất một bước ngược-một bước xuôi, khoanh vùng recall và cấp dữ liệu đáng tin cậy cho Sales/CEO.

Không nên nối ERP trực tiếp vào PLC. Dùng ranh giới ISA-95: business/ERP (level 4) gửi kế hoạch và nhận kết quả từ MES/MOM (level 3); gateway OT đọc telemetry từ máy (levels 0–2) qua OPC UA rồi phát sự kiện đã chuẩn hóa. ISA-95 xác định riêng interface giữa level 3 và 4; OPC UA có mô hình thông tin, giao tiếp, bảo mật và interoperability cho sensor, control, MES và ERP. [ISA-95](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard) · [OPC UA Part 1](https://reference.opcfoundation.org/specs/OPC-10000-1/4)

## Bằng chứng chuẩn gốc và hệ quả thiết kế

| Chủ đề | Bằng chứng | Hệ quả áp dụng cho Nacoms |
|---|---|---|
| An toàn thực phẩm | [Codex CXC 1-1969](https://www.fao.org/fao-who-codexalimentarius/sh-proxy/pt/?lnk=1&url=https%253A%252F%252Fworkspace.fao.org%252Fsites%252Fcodex%252FStandards%252FCXC%2B1-1969%252FCXC_001e.pdf) yêu cầu chương trình tiên quyết/GHP hoạt động và được xác minh trước khi HACCP hiệu quả. | Số hóa GHP/SSOP, checklist vệ sinh, calibration, đào tạo và QC như control records; không chỉ tạo màn hình CCP. Mỗi deviation phải có disposition, người phê duyệt và bằng chứng khắc phục. |
| FSMS/HACCP | [ISO 22000:2018](https://www.iso.org/standard/65464.html) kết hợp giao tiếp, quản trị hệ thống, PRP và nguyên tắc HACCP; tiêu chuẩn vẫn được ISO xác nhận năm 2023. | Recipe/routing version phải được phê duyệt; lệnh sản xuất lưu version đã hiệu lực, thông số giới hạn, kết quả kiểm soát và release/hold. Không cho sửa hồi tố kết quả batch. |
| Truy xuất food chain | [ISO 22005:2007](https://www.iso.org/standard/36297.html), được ISO xác nhận năm 2022, nêu nguyên tắc/yêu cầu cơ bản để thiết kế traceability và xác định lịch sử/vị trí của sản phẩm hoặc thành phần. | Định danh `Lot` xuyên suốt nguyên liệu, WIP, thành phẩm; mô hình quan hệ nhiều-nhiều `LotTransformation` để một batch dùng nhiều input lot và sinh nhiều output lot. |
| Interoperability chuỗi cung ứng | [GS1 Global Traceability Standard](https://www.gs1.org/standards/gs1-global-traceability-standard/current-standard) quy định CTE (sự kiện theo dõi trọng yếu) và KDE (dữ liệu mô tả sự kiện), đồng thời nêu trace back nhà cung cấp trực tiếp/track forward người nhận trực tiếp. | Chuẩn hoá các CTE: receive, QC, quarantine/release, issue, transform, pack, move, ship, return/recall. KDE tối thiểu: ai, cái gì, ở đâu, khi nào, vì sao, lượng, đơn vị, chứng từ, thiết bị/line, lot cha-con. Dùng barcode/QR trước, RFID chỉ khi bài toán vận hành chứng minh được ROI. |
| Batch control | [ISA-88](https://www.isa.org/standards-and-publications/isa-standards/isa-standards-committees) định nghĩa model/thuật ngữ batch, hướng đến kiến trúc modular, scalable. | Công thức gồm master recipe, phiên bản, nguyên liệu/định mức, các phase (sơ chế, sấy, đóng gói), parameter set và quality checkpoints; production batch chỉ là bản thực thi đã đóng băng của recipe version. |
| Máy móc và ERP/MES | [ISA-95/IEC 62264](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard) phân tách Level 0–2 (quy trình/sensor/control), Level 3 (MOM/MES) và Level 4 (ERP/business); mục tiêu interface là giảm lỗi, chi phí và rủi ro tích hợp. | Có `Equipment`, `EquipmentClass`, line, capability, status, maintenance plan và machine event ở MOM. Sales/CEO nhận read model/KPI; không đọc trực tiếp database vận hành và không điều khiển PLC qua UI ERP ở phase đầu. |
| Telemetry có ngữ nghĩa | [OPC UA](https://reference.opcfoundation.org/specs/OPC-10000-1/4) hỗ trợ trao đổi thông tin từ thiết bị tới enterprise với information model, message, communication và conformance model. | Tạo adapter/gateway theo từng hãng máy; map tag vendor thành contract chuẩn (`temperature`, `humidity`, `run-state`, `energy`, `alarm`). Lưu raw telemetry tách khỏi event nghiệp vụ đã xác nhận; mất kết nối không được tự suy diễn batch hoàn tất. |
| KPI/OEE | [ISO 22400-1:2014](https://www.iso.org/cms/%20render/live/en/sites/isoorg/contents/data/standard/05/68/56847.html), ISO xác nhận năm 2025, cung cấp framework KPI trung lập cho MOM ở batch, continuous và discrete manufacturing. | Định nghĩa KPI và mẫu số ngay từ data contract: planned production time, run time, downtime reason, good quantity, reject quantity và timestamp. OEE chỉ hiển thị khi đủ dữ liệu Availability, Performance, Quality; không dùng dữ liệu thủ công/thiếu làm “sự thật CEO”. |

## Ánh xạ tiêu chuẩn vào 7 nghiệp vụ

| Nghiệp vụ | Chuẩn neo chính | Điều hệ thống bắt buộc phải làm |
|---|---|---|
| Nghiên cứu và công thức | ISO 22000, Codex CXC 1-1969, ISA-88 | Quản lý `RecipeVersion`, thông số mục tiêu, PRP/CCP checkpoint, phê duyệt và snapshot khi đưa vào sản xuất. |
| Thu mua và nguyên liệu | ISO 22005, GS1 GTS, Codex CXC 1-1969 | Định danh lô khi nhận hàng, QC/disposition trước khi dùng, lưu nguồn gốc nhà cung cấp và chứng từ nhận hàng. |
| Sản xuất sơ chế - sấy - đóng gói | ISO 22000, ISA-95, OPC UA, ISA-88 | Tách lệnh/kế hoạch khỏi batch thực thi; ghi input-output-lot, machine context, quality checkpoint và genealogy tại từng công đoạn. |
| Máy móc và bảo trì | ISA-95, OPC UA, ISO 22400-1 | Chuẩn hóa machine state, downtime reason, runtime meter và đưa telemetry vào MOM mà không cho ERP điều khiển máy trực tiếp. |
| Thành phẩm và kho | ISO 22005, GS1 GTS | Quản lý lot thành phẩm, FEFO, reservation, ship/return/hold và khả năng recall một bước ngược - một bước xuôi. |
| Hao hụt và giá thành | ISO 22400-1, ISA-95 | Tính KPI từ dữ liệu gốc của operation/batch; tách hao hụt quy trình, phế phẩm, rework, chênh lệch kiểm kê và downtime. |
| Sales và CEO | ISO 22400-1, ISA-95 | Chỉ đọc projection đáng tin: ATP, yield, OEE, margin, recall impact, stock risk; không đọc trực tiếp transaction xưởng. |

## Mô hình liên kết tối thiểu

```text
Supplier / Farm ─Receive/QC─> RawMaterialLot ─Issue─┐
                                                     ├─ ProductionBatch (recipe + routing version)
Equipment/Line ─MachineEvent/parameter───────────────┘
  ProductionBatch ─Transform (pre-process → dry → pack)─> WipLot / FinishedGoodLot
  FinishedGoodLot ─Move/Ship─> Warehouse / CustomerOrder
  QC, hold/release, deviation, waste ────────────────^ (gắn lot/batch/công đoạn)
```

`LotTransformation` phải ghi lượng input đã cân, output đạt, reject/by-product/waste, đơn vị chuẩn, thời điểm, station và nguyên nhân hao hụt. Lượng thực tế là dữ liệu gốc; các chỉ số yield và hao hụt là read model có thể tính lại:

`yield (%) = output_good / input_measured × 100`  
`hao hụt (%) = (input_measured − output_good − approved_byproduct) / input_measured × 100`

Chỉ áp dụng công thức khi các lượng đã được đổi về đơn vị/quy tắc cùng loại. Cần phân biệt hao hụt quy trình kế hoạch, hao hụt thực tế, phế phẩm, rework và chênh lệch kiểm kê; không gộp chúng thành một số “loss”.

## Contract và tự động hóa an toàn

1. **Master data có version:** item, UoM/conversion, supplier, location, equipment, recipe, routing, QC specification và reason code. Một lệnh/batch luôn trỏ immutable version.
2. **Sự kiện nghiệp vụ append-only:** event có `eventId`, `occurredAt`, `recordedAt`, actor/source, correlation id, schema version và idempotency key. Sửa sai bằng event điều chỉnh có lý do/phê duyệt, không update xóa dấu vết.
3. **Outbox + consumer idempotent:** khi batch released, stock/available-to-promise, cost, recall index và CEO read model được cập nhật qua event; retry không nhân đôi tồn kho hay hao hụt.
4. **Tự động hóa theo mức rủi ro:** cảnh báo điều kiện sấy vượt limit và tạo deviation tự động; auto-hold chỉ khi policy được QA phê duyệt; release, recipe change, stock adjustment và lệnh điều khiển máy vẫn cần phân quyền/phê duyệt rõ ràng.
5. **OT boundary:** gateway chỉ có quyền đọc mặc định; xác thực riêng, network segmentation, audit command và manual fallback. OPC UA nêu rõ yêu cầu về security, authentication và auditing trong kiến trúc của nó; kết nối máy làm tăng phạm vi an toàn/availability, không phải chỉ là integration API. [OPC Foundation overview](https://opcfoundation.org/about/opc-technologies/opc-ua/)

## Hệ quả thực thi cụ thể cho Nacoms

1. Không thể tính hao hụt đáng tin nếu `LotTransformation` chỉ được ghi ở cuối mẻ; hệ thống phải cho phép ghi đầu vào/đầu ra tại từng operation.
2. Không thể làm recall nhanh nếu một thành phẩm chỉ tham chiếu một `productionOrder`; cần liên kết nhiều-nhiều giữa input lots và output lots.
3. Không nên lấy OEE từ số tổng hợp cuối ngày; phải thu thập `planned production time`, `run time`, `good count`, `reject count` và `downtime reason` theo timestamp.
4. Không nên cho Sales hứa hàng từ tồn kho tổng; ATP phải dựa trên lot đã `Released`, reservation hiện tại và hạn dùng.
5. Không nên nối thẳng máy sấy vào ERP; nên có adapter/gateway OT hoặc MOM edge để chuẩn hóa tag và chống lệ thuộc vendor.

## Lộ trình triển khai có thể kiểm chứng

| Pha | Deliverable | Gate nghiệm thu |
|---|---|---|
| 0 — nền dữ liệu | lot, UoM, location, recipe/routing version, audit/outbox, quyền theo nhà máy | Một raw lot được nhận/QC/cấp phát và không thể sửa lịch sử không dấu vết. |
| 1 — batch & hao hụt | lệnh sản xuất, transformation, WIP/finished lot, mass balance, hold/release | Truy từ một thành phẩm về tất cả input lots và ngược lại trong dữ liệu thử nghiệm; yield tính lại khớp với cân thực tế. |
| 2 — máy & chất lượng | equipment, maintenance, downtime/deviation, OPC UA gateway pilot read-only | Một dryer pilot phát telemetry; event bị trùng/mất mạng không làm sai batch; cảnh báo có audit trail. |
| 3 — điều hành & thương mại | cost/read models, ATP, recall drill, Sales/CEO dashboard | Dashboard có lineage tới event nguồn; diễn tập recall khoanh vùng được lot, tồn và khách bị ảnh hưởng. |

## Giới hạn và quyết định còn mở

- Cần xác nhận quy định Việt Nam/thị trường xuất khẩu, giới hạn vi sinh/độ ẩm, thời hạn lưu hồ sơ và quy tắc release của từng nhóm sản phẩm trước khi đóng QC specification.
- OEE không thay thế yield/hao hụt thực phẩm: OEE đo hiệu quả thiết bị; yield đo chuyển đổi khối lượng nguyên liệu. CEO cần xem cả hai cùng downtime, reject, cost và tồn kho.
- Chưa nên chọn MES/SCADA, broker hay thiết bị barcode/RFID trước khi khảo sát dây chuyền, giao thức máy sấy và quy trình cân hiện hữu.

## Nguồn chính thức

- [FAO/WHO Codex — General Principles of Food Hygiene, CXC 1-1969](https://www.fao.org/fao-who-codexalimentarius/sh-proxy/pt/?lnk=1&url=https%253A%252F%252Fworkspace.fao.org%252Fsites%252Fcodex%252FStandards%252FCXC%2B1-1969%252FCXC_001e.pdf)
- [ISO 22000:2018 — Food safety management systems](https://www.iso.org/standard/65464.html)
- [ISO 22005:2007 — Traceability in the feed and food chain](https://www.iso.org/standard/36297.html)
- [GS1 Global Traceability Standard](https://www.gs1.org/standards/gs1-global-traceability-standard/current-standard)
- [ISA-95 — Enterprise-Control System Integration](https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard)
- [OPC UA Part 1 — Overview and Concepts](https://reference.opcfoundation.org/specs/OPC-10000-1/4)
- [ISO 22400-1:2014 — Manufacturing operations KPIs](https://www.iso.org/cms/%20render/live/en/sites/isoorg/contents/data/standard/05/68/56847.html)
