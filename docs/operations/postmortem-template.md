# Mẫu Post-Mortem — His.Hope EMR

> **Tài liệu:** OPS-PM-001
> **Version:** 1.0
> **Audience:** SRE, Engineering Manager, CTO
> **Cập nhật:** 2026-07-23

---

## Hướng dẫn sử dụng

- Viết post-mortem trong vòng **24-48 giờ** sau khi sự cố được khắc phục
- Ưu tiên **blameless** — tập trung vào hệ thống, không phải cá nhân
- Tất cả action items phải có người phụ trách và deadline
- Sau khi hoàn thành, gửi lên Slack `#his-hope-incidents` và `#his-hope-announcements`

---

# Post-Mortem: [Tên sự cố ngắn gọn]

**Ngày:** YYYY-MM-DD
**Thời gian phát hiện:** HH:MM UTC+7
**Thời gian khắc phục:** HH:MM UTC+7
**Thời gian phục hồi hoàn toàn:** HH:MM UTC+7
**Mức độ:** P0 / P1 / P2 / P3
**Incident Commander:** [Tên]
**Tác giả Post-Mortem:** [Tên người viết]
**Người tham gia:** [Danh sách tên — ai đã hỗ trợ trong quá trình xử lý]

## Tóm tắt

[2-3 câu mô tả sự cố: cái gì xảy ra, ảnh hưởng đến ai, và kết quả cuối cùng]

Ví dụ: *Lúc 14:30, patient-service bắt đầu trả về lỗi 500 do connection pool đến CockroachDB
cạn kiệt. Khoảng 1,200 request bị ảnh hưởng trong 12 phút. Nguyên nhân gốc là
cấu hình `MaxPoolSize=10` quá thấp cho traffic peak giờ hành chính.*

## Timeline (UTC+7)

| Thời gian | Sự kiện |
|-----------|---------|
| HH:MM | [Alert nào đã fire — ai nhận alert, ai response đầu tiên] |
| HH:MM | [Bắt đầu triage, chẩn đoán ban đầu là gì] |
| HH:MM | [Xác định root cause hoặc hypothesis chính] |
| HH:MM | [Mitigation áp dụng: restart, scale, rollback, config change...] |
| HH:MM | [Service restored — bắt đầu nhận traffic bình thường] |
| HH:MM | [Xác nhận tất cả metrics/SLO trở về baseline] |
| HH:MM | [Incident closed, PagerDuty resolved] |

## Nguyên nhân gốc (Root Cause)

[Mô tả kỹ thuật chi tiết: cái gì thực sự gây ra sự cố. Tại sao không phát hiện sớm hơn.
Có liên quan đến code, config, infrastructure, hoặc process nào.]

Ví dụ: *Connection pool giữa patient-service và CockroachDB được cấu hình
`MaxPoolSize=10`. Trong giờ cao điểm (14:00-15:00), số lượng concurrent requests
vượt quá 10, dẫn đến `Npgsql.PostgresException: 53300: too many clients`. Pool
bị exhausted do không có circuit breaker ở tầng DB access. Không có pre-production
load test với traffic profile tương tự production.*

### Trigger
[Sự kiện cụ thể kích hoạt sự cố: deploy lúc mấy giờ, spike traffic, expired cert...]

### Contributing factors
- [Yếu tố góp phần 1 — không có circuit breaker]
- [Yếu tố góp phần 2 — thiếu load test]
- [Yếu tố góp phần 3 — monitoring không cảnh báo connection pool đến khi exhausted]

## Impact

| Metric | Giá trị |
|--------|---------|
| Thời gian downtime / degraded | [N] phút |
| Số request bị ảnh hưởng (5xx/timeout) | [N] |
| Error budget consumed | [N]% của tháng |
| SLI availability impact | Giảm từ [X]% → [Y]% |
| Người dùng bị ảnh hưởng | [Mô tả: tất cả / chỉ users ở region X / chỉ login flow...] |
| Dữ liệu bị mất (nếu có) | [Số records, dung lượng] |

## Detection

- **Phương thức phát hiện:** [PagerDuty alert / user report / monitoring dashboard / tình cờ phát hiện]
- **Alert đã fire:** [Tên alert, severity]
- **Thời gian từ lúc xảy ra → lúc phát hiện (TTD):** [N] phút
- **Nếu không có alert:** [Tại sao? Cần thêm alert gì?]

## Resolution

[Mô tả các bước đã làm để khắc phục — đủ chi tiết để người khác làm theo nếu sự cố lặp lại]

```
1. [Step — ai làm, lúc mấy giờ]
2. [Step]
3. [Step]
4. [Verify: metric nào xác nhận đã khắc phục]
```

## Lessons Learned

### Điểm tích cực (What went well)

- [Ví dụ: On-call engineer phản hồi trong vòng 2 phút]
- [Ví dụ: Dashboard hiển thị rõ connection pool status]
- [Ví dụ: Rollback script có sẵn, rollback thành công trong 3 phút]

### Điểm cần cải thiện (What went wrong)

- [Ví dụ: Alert không fire vì thiếu metric cho connection pool usage]
- [Ví dụ: Runbook không cập nhật cho tình huống này]
- [Ví dụ: Mất 5 phút mới xác định được service nào fail]

### Điểm bất ngờ (Where we got lucky)

- [Ví dụ: Linkerd circuit breaker tự động mở và giảm blast radius]
- [Ví dụ: Redis cache vẫn trả dữ liệu cũ, FE hiển thị stale data thay vì lỗi]

## Action Items (Đăng ký trong Jira)

| # | Hành động | Loại | Người phụ trách | Deadline | Jira |
|---|-----------|------|----------------|----------|------|
| 1 | [Mô tả cụ thể, actionable] | prevent / detect / mitigate | [Tên] | YYYY-MM-DD | [HIS-XXXX] |
| 2 | [Mô tả] | prevent / detect / mitigate | [Tên] | YYYY-MM-DD | [HIS-XXXX] |
| 3 | [Mô tả] | prevent / detect / mitigate | [Tên] | YYYY-MM-DD | [HIS-XXXX] |

*Loại action: **prevent** = ngăn tái diễn, **detect** = phát hiện sớm hơn, **mitigate** = giảm impact*

## Phụ lục

### Timeline chi tiết (Logs, screenshots)

```
[Đính kèm screenshots từ Grafana, Kibana, PagerDuty timeline nếu có]
```

### Links tham khảo

- PagerDuty incident: [URL]
- Slack thread: [URL]
- Grafana snapshot (thời điểm incident): [URL]
- Kibana query: [URL]
- PR liên quan: [URL]
