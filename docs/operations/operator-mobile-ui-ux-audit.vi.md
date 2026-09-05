# Audit UI/UX operator-mobile

Ngày audit: 2026-08-29

## Kết luận điều hành

Operator-mobile hiện không có bằng chứng về lỗi tràn ngang trong các luồng E2E đã chạy. Tuy nhiên, ba màn hình nghiệp vụ chính đang có rủi ro UX rõ ràng khi sử dụng trên điện thoại:

| Màn hình | Mức khó dùng khi trượt dọc | Nguyên nhân chính | Ưu tiên |
| --- | --- | --- | --- |
| Production | Cao | Nhiều khối KPI, SOP, measurement, loss review và form ghi nhận nằm trong một luồng dài | P0 |
| Quality | Cao | Inspection, test, sample, disposition, deviation và review nằm chung một card | P0 |
| Maintenance | Trung bình đến cao | Health, calibration, telemetry, plans xuất hiện trước work order và checklist | P1 |
| Traceability | Trung bình | Luồng scan tốt nhưng sau khi mở lot có nhiều lịch sử nối tiếp | P1 |
| Handover | Thấp đến trung bình | Bốn nhóm thông tin, ít thao tác chỉnh sửa | P2 |
| Notifications | Thấp | Danh sách và hai hành động chính dễ hiểu | P2 |
| Sync | Thấp đến trung bình | Danh sách queue có thể dài, thao tác discard dễ gây nhầm | P1 |

Nhận định này dựa trên source layout, route, style và E2E hiện có. E2E đã xác minh không tràn ngang, các route và API chính trả về thành công, nhưng chưa đo được scroll depth, thời gian hoàn thành thao tác bằng tay, vị trí bàn tay khi nhập liệu hoặc hành vi bàn phím trên Android/iOS. Vì vậy chưa nên gọi đây là kết quả usability test trên thiết bị thật.

## Tiêu chuẩn đánh giá

Operator thường đứng tại dây chuyền, dùng một tay, có thể đeo găng và cần ghi nhận nhanh. Mỗi màn hình cần ưu tiên một nhiệm vụ chính, giữ ngữ cảnh gần trường nhập liệu và không bắt người dùng nhớ dữ liệu đã chọn ở phần đầu trang.

Các tiêu chí cần dùng khi implement:

- Nhiệm vụ chính phải bắt đầu trong viewport đầu tiên sau tiêu đề, không bị đẩy xuống dưới các KPI phụ.
- Nút hoàn tất hoặc ghi nhận phải ở cùng vùng thao tác với form, có thể dùng sticky action bar nhưng phải chừa safe area và không che trường cuối.
- Mỗi màn hình chỉ nên có một hành động primary tại một thời điểm.
- Nội dung phụ như KPI, lịch sử, telemetry, cost và review phải thu gọn mặc định hoặc chuyển thành màn hình chi tiết.
- Control tối thiểu 44 x 44 CSS px, khoảng cách giữa các control đủ để tránh bấm nhầm.
- Khi bàn phím mở, trường đang nhập và lỗi validation phải nằm trong vùng nhìn thấy.
- Sau khi lưu phải giữ lại batch, lot, machine hoặc work order đang chọn; không bắt chọn lại.
- Trạng thái loading, offline pending, conflict và lỗi phải xuất hiện gần hành động vừa thực hiện.
- Không tự động cuộn ngang hoặc hijack thao tác cuộn dọc. Native mobile cần ưu tiên scroll tự nhiên, ổn định và có reduced-motion.

## Audit theo từng màn hình

### 1. Shell và điều hướng

Hiện trạng đã xác minh:

- Header sticky, context tenant ở đầu trang và bottom navigation fixed.
- Nội dung đã có padding phía dưới theo chiều cao bottom navigation và safe area.
- Có 5 mục bottom navigation: Production, Traceability, Quality, Maintenance, Sync.
- Menu phụ chứa Sync, Handover, Notifications, theme, bảo mật và logout.
- E2E đã kiểm tra ở viewport 390 x 844 và không phát hiện tràn ngang.

Vấn đề UX:

- Header, tenant context và bottom navigation chiếm nhiều chiều cao cố định. Trên màn hình thấp, vùng nội dung hữu ích bị co lại trước khi người dùng bắt đầu thao tác.
- Tenant context nằm trên trang và không sticky cùng header. Khi cuộn sâu, người dùng không còn thấy tenant hiện tại.
- Menu tài khoản đang gánh cả chức năng vận hành phụ, bảo mật và giao diện. Handover và Notifications có thể bị bỏ sót.
- Bottom navigation có thể chứa nhãn dài khi đổi ngôn ngữ, làm giảm khả năng quét nhanh.

Đề xuất:

- Giữ 4 khu vực nghiệp vụ ở bottom navigation: Production, Quality, Maintenance, Traceability. Đưa Sync vào một trạng thái badge hoặc menu công cụ, không nhất thiết chiếm một tab nếu queue rỗng.
- Hiển thị tenant dạng compact trong header, mở selector bằng bottom sheet khi chạm. Không giữ một block context cao ở đầu mọi màn hình.
- Thêm trạng thái offline/pending dạng banner nhỏ có thể mở trang Sync.
- Tách menu “Công cụ ca” gồm Handover và Notifications khỏi menu tài khoản/bảo mật.
- Khi màn hình có form dài, bottom navigation vẫn giữ nguyên nhưng primary action phải nằm trong sticky action bar phía trên nav.

### 2. Production

Hiện trạng source:

- Trước form ghi nhận có các khối Production summary, KPI, OEE, exceptions, costs và workflow.
- Sau khi chọn batch, cùng một card chứa process step, input/output quantity, QC status, SOP, SOP artifact, batch actions, measurement và loss review.
- Nút “Record operation” nằm sau các khối tùy chọn.

Đánh giá: khó dùng cao. Đây là màn hình có scroll depth và tải nhận thức lớn nhất. Người vận hành vào để ghi output nhưng phải đi qua nhiều thông tin quản trị trước khi đến form. Khi chọn batch, chiều cao trang tăng động, làm vị trí các nút thay đổi.

Đề xuất luồng mới:

1. Chọn batch ở đầu màn hình, hiển thị batch đang chọn cùng trạng thái.
2. Hiển thị form tối thiểu: process step, output quantity, QC status.
3. Đặt “Record operation” trong sticky action bar khi form có dữ liệu hợp lệ.
4. Đưa input quantity, measurement và loss review vào các section thu gọn, mở khi người dùng cần.
5. KPI, OEE, cost và exceptions chuyển thành “Shift overview” thu gọn hoặc trang riêng.
6. SOP hiển thị dạng summary một dòng, có nút “Xem hướng dẫn”. SOP artifact và acknowledgment mở bottom sheet hoặc route chi tiết.

Tiêu chí nghiệm thu:

- Từ khi mở Production đến khi nhìn thấy batch selector và trường output không cần cuộn trên viewport 390 x 844.
- Với batch đã chọn, nút Record operation luôn có thể truy cập mà không phải cuộn về cuối.
- Không có quá một primary action visible trong cùng viewport.
- Các section phụ không làm thay đổi vị trí form chính ngoài section đang mở.
- Test bằng bàn phím mobile: input quantity không bị keyboard che, lỗi nằm ngay dưới trường lỗi.

### 3. Quality

Hiện trạng source:

- Một field card chứa inspection cơ bản, quality test, sample, sample disposition, deviation và second-person review.
- Nút Save inspection nằm giữa form test và các luồng sample/deviation.
- Các fieldset nối tiếp nhau và chỉ xuất hiện thêm khi dữ liệu liên quan đã load.

Đánh giá: khó dùng cao. Màn hình đang gom nhiều vai trò và nhiều thời điểm làm việc. Người kiểm tra có thể chỉ cần ghi inspection nhưng phải lướt qua cấu trúc sample và deviation. Khi dữ liệu load thêm, người dùng có thể mất vị trí đang đọc.

Đề xuất:

- Tách thành flow theo nhiệm vụ với tab hoặc segmented control: Inspection, Sample, Deviation, Review.
- Tab Inspection chỉ gồm lot, plan, inspector, moisture, status, test cơ bản và Save inspection.
- Sau khi lưu inspection, hiển thị inspection vừa lưu cùng CTA rõ ràng “Tạo sample” thay vì mở toàn bộ form sample bên dưới.
- Sample disposition và deviation review chỉ hiện sau khi đã chọn item tương ứng.
- Giữ draft cục bộ trong session để đổi tab không mất dữ liệu.
- Dùng step summary ở đầu: Lot, Inspection status, Sample status, Deviation status.

Tiêu chí nghiệm thu:

- Mỗi tab có chiều cao phù hợp một nhiệm vụ và primary action ở cuối section đang mở.
- Không cần cuộn qua sample/deviation để lưu inspection cơ bản.
- Sau khi lưu, trạng thái synced hoặc pending sync nằm trong cùng viewport với nút vừa bấm.
- Chuyển tab không làm mất dữ liệu chưa gửi và không gọi lại toàn bộ màn hình nếu không cần.

### 4. Maintenance

Hiện trạng source:

- Trong cùng article có machine health, machine selector, calibration, telemetry, preventive plans, work order, technician, evidence, checklist, complete work order và downtime control.
- Trên màn hình nhỏ, label đã chuyển thành layout dọc, đây là điểm tốt cho khả năng đọc.
- Nút Complete work order bị khóa đến khi checklist hoàn tất.

Đánh giá: trung bình đến cao. Layout field đã phù hợp mobile hơn các màn hình khác, nhưng thứ tự ưu tiên chưa theo nhiệm vụ. Người dùng cần chọn work order sớm hơn health, calibration và telemetry. Checklist và completion ở sâu cuối trang.

Đề xuất:

- Đổi thứ tự: Machine, Work order, task summary, checklist, evidence, Complete work order.
- Health, calibration, telemetry và plans thành các disclosure “Thông tin máy” đóng mặc định.
- Khi chọn work order, chỉ load checklist và dữ liệu liên quan; không bắt người dùng đọc toàn bộ telemetry trước.
- Sticky action “Complete work order” với trạng thái disabled và lý do ngắn: “Còn 2 mục checklist”.
- Capture evidence nên là action cạnh checklist hoặc mỗi mục checklist có thể gắn evidence, tránh phải nhớ evidence reference ở một trường xa.
- Downtime control chuyển thành action riêng sau completion hoặc modal rõ ngữ cảnh, tránh cạnh tranh với nút hoàn tất.

Tiêu chí nghiệm thu:

- Người dùng thấy Machine và Work order trong viewport đầu tiên.
- Khi checklist chưa đủ, lý do disabled hiển thị ngay cạnh CTA.
- Khi checklist đủ, CTA hoàn tất không bị đẩy xuống cuối trang.
- Calibration/telemetry không được làm thay đổi vị trí checklist đang thao tác.

### 5. Traceability

Hiện trạng source:

- Có CTA Scan QR, chọn lot, Open lot và sau đó tải quality history, status history, inventory history, availability, FEFO, recall impact và genealogy.
- Đây là màn hình có hướng dẫn đầu vào rõ nhất và dùng đúng native QR seam.

Đánh giá: trung bình. Luồng scan/open lot hợp lý, nhưng kết quả sau khi mở lot là một chuỗi article dài. Thông tin nguy hiểm nhất, như recall impact hoặc Hold/Rejected, không nên bị đẩy dưới nhiều lịch sử.

Đề xuất:

- Sau scan, hiển thị lot identity và disposition ở đầu kết quả.
- Nếu lot Hold, Rejected hoặc có recall impact, đưa cảnh báo lên ngay dưới identity.
- Chia kết quả thành Summary, Genealogy, Quality, Inventory, FEFO bằng tabs hoặc disclosure.
- Giữ “Change disposition” gần summary, yêu cầu reason/evidence khi trạng thái nhạy cảm.
- Cho phép scan lot tiếp theo bằng CTA cố định sau khi hoàn tất xem lot.

Tiêu chí nghiệm thu:

- Kết quả lot và cảnh báo recall/disposition hiển thị không cần cuộn.
- Lịch sử dài không mở đồng thời tất cả section.
- Không mất lot đang xem khi mở hoặc đóng section.

### 6. Handover

Hiện trạng source:

- Có refresh và bốn nhóm Started batches, Lots on hold, Open downtime, Overdue work orders.
- Grid chuyển từ 2 cột xuống 1 cột ở màn hình hẹp.

Đánh giá: thấp đến trung bình. Đây là màn hình đọc nhanh, không có form dài. Tuy nhiên, danh sách trong mỗi card có thể kéo dài và chưa có hành động trực tiếp để mở item hoặc đánh dấu đã xử lý.

Đề xuất:

- Giữ 4 summary count ở trên, sau đó chỉ hiển thị các item có rủi ro.
- Mỗi item nên là một target chạm mở thẳng đến Production, Traceability hoặc Maintenance với filter tương ứng.
- Thêm “Copy handover summary” hoặc “Confirm handover” nếu nghiệp vụ cần, nhưng không thêm nếu backend chưa có contract.

### 7. Notifications

Hiện trạng source:

- Có refresh, mark all read và danh sách notification có trạng thái unread.
- Item hỗ trợ click và Enter.

Đánh giá: thấp. Luồng ngắn và dễ hiểu. Rủi ro chính là nhiều thông báo có thể làm trang dài, còn thao tác đánh dấu đọc đang dùng item role button nhưng chưa có affordance rõ như chevron hoặc thời điểm tương đối.

Đề xuất:

- Giữ danh sách phẳng, thêm phân nhóm Today/Earlier khi số lượng lớn.
- Sau khi đánh dấu đọc, giữ focus và thông báo ngắn qua live region.
- Nếu notification dẫn đến nghiệp vụ, thêm deep link trực tiếp thay vì chỉ mark read.

### 8. Sync

Hiện trạng source:

- Có Sync now, trạng thái message, empty state và từng queue entry với retry/discard.
- Queue có thể chứa nhiều record và mỗi entry hiển thị endpoint.

Đánh giá: thấp đến trung bình. Trang dễ hiểu khi queue nhỏ, nhưng endpoint kỹ thuật không phải lúc nào cũng có ý nghĩa với operator. Discard là thao tác mất dữ liệu cục bộ nên cần xác nhận và giải thích hậu quả.

Đề xuất:

- Hiển thị tên nghiệp vụ, ví dụ “Ghi nhận sản xuất”, thay cho endpoint raw.
- Nhóm queue theo Pending, Failed, Conflict, Synced history; mặc định mở Failed/Conflict trước.
- Retry từng record và Retry all failed.
- Discard phải có confirm dialog ghi rõ record sẽ bị xóa khỏi thiết bị và không gửi lên server.
- Hiển thị last sync, số pending và conflict ở đầu trang.

## Kế hoạch cải thiện 6 bước

### Bước 1: Chuẩn hóa shell mobile

Giảm chiều cao context tenant, phân loại menu công cụ và menu tài khoản, chuẩn hóa safe area, touch target và vùng sticky action.

### Bước 2: Tách nhiệm vụ chính khỏi dữ liệu phụ

Production, Quality và Maintenance phải mở đúng form hành động trước. KPI, telemetry, cost, history và review chuyển thành disclosure hoặc route chi tiết.

### Bước 3: Thêm sticky primary action theo ngữ cảnh

Mỗi flow chỉ có một CTA chính. Sticky bar phải chừa bottom navigation, safe area và trạng thái loading/pending/conflict.

### Bước 4: Giảm thay đổi layout động

Khi API load thêm dữ liệu, dùng skeleton có kích thước tương ứng hoặc vùng reserved. Không chèn nhiều card vào trước field người dùng đang nhập.

### Bước 5: Chuẩn hóa lỗi, offline và recovery

Lỗi đặt cạnh field hoặc action. Pending sync, conflict và retry có thông điệp nghiệp vụ. Discard luôn có xác nhận. Không dùng toast đơn độc cho lỗi cần hành động.

### Bước 6: Validate trên thiết bị thật

Chạy ma trận Android nhỏ 360 x 800, Android 390 x 844, iPhone có safe area và tablet. Đo task completion, scroll depth, số lần quay lại đầu trang, keyboard occlusion, touch target, dark/light, vi/en và reduced motion.

## Bộ test UX cần bổ sung

- Production: ghi một operation mới bằng một tay, khi online và offline.
- Quality: lưu inspection cơ bản, sau đó tạo sample, không mất draft khi đổi tab.
- Maintenance: chọn work order, tick checklist, capture evidence, hoàn tất.
- Traceability: scan lot, nhận biết recall/Hold ngay, mở genealogy, đổi disposition.
- Sync: retry conflict, discard có confirm, khôi phục sau mất mạng.
- Shell: mở menu khi ở cuối trang, keyboard focus, safe area, tenant switch không mất form.

Mỗi test cần thu các chỉ số: thời gian hoàn thành, số lần scroll, khoảng cách scroll tổng, lỗi bấm nhầm, lỗi validation, thời điểm CTA xuất hiện và tỷ lệ hoàn tất không cần trợ giúp.

## Giới hạn bằng chứng hiện tại

- Đã có source và E2E contract cho route, API, locale, theme và không tràn ngang.
- Chưa có bằng chứng screenshot/runtime cho toàn bộ trạng thái dữ liệu dài ở viewport thật.
- Chưa có native Android/iOS instrumentation cho keyboard, camera, safe area và thao tác một tay.
- Do đó các mức “khó dùng” ở trên là audit heuristic có căn cứ từ cấu trúc màn hình, cần xác nhận lại bằng usability test trước khi chốt số đo UX.

