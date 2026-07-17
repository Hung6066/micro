### Task 2: Create TEMPLATE.md

**Files:**
- Create: `docs/knowledge/TEMPLATE.md`

**Interfaces:**
- Produces: `TEMPLATE.md` — reference for humans and agents creating new entries

- [ ] **Step 1: Write TEMPLATE.md**

```markdown
---
id: {domain}-{type}-{nn}
type: gotcha|pattern|decision
domain: {domain-name}
tags: [tag1, tag2]
severity: critical|warning|info
agent: @agent-name
author: @agent-name
date: YYYY-MM-DD
related: []
---

# [Title — mô tả ngắn gọn bài học]

## [Section 1]

[Nội dung]

## [Section 2]

[Nội dung]

---

## Template by Type

### Gotcha (`type: gotcha`)

Sections bắt buộc:
- **Vấn đề** (Problem) — mô tả lỗi đã xảy ra
- **Hậu quả** (Consequence) — điều gì xảy ra nếu không fix
- **Cách phát hiện** (Detection) — grep command, test, hoặc dấu hiệu
- **Cách làm đúng** (Correct Approach) — code mẫu đúng + sai
- **Đã xảy ra ở đâu** (Where It Happened) — service, thời gian

### Pattern (`type: pattern`)

Sections bắt buộc:
- **Khi nào dùng** (When To Use) — điều kiện áp dụng
- **Mẫu chuẩn** (Standard Template) — code mẫu
- **Lý do** (Rationale) — tại sao dùng mẫu này
- **Tham khảo** (References) — file hoặc doc liên quan

### Decision (`type: decision`)

Sections bắt buộc:
- **Quyết định** (Decision) — đã chọn gì
- **Lý do** (Rationale) — tại sao
- **Trade-off đã cân nhắc** (Trade-offs) — bảng so sánh
- **Khi nào xem xét lại** (When To Revisit) — điều kiện đổi quyết định
- **Tham khảo** (References) — ADR hoặc doc liên quan

## Quy tắc chung

1. **1 entry = 1 bài học** — không gộp nhiều vấn đề vào 1 file
2. **Có ví dụ code** — luôn kèm code mẫu đúng/sai
3. **Ghi rõ hậu quả** — nếu không tuân thủ thì sao?
4. **Tối thiểu 2 tags** — 1 tech + 1 problem/concept
5. **Không trùng lặp** — kiểm tra INDEX.md trước khi tạo
6. **Đặt tên file**: `{domain}-{type}-{nn}-{slug}.md`
```

- [ ] **Step 2: Commit**

```bash
git add docs/knowledge/TEMPLATE.md
git commit -m "docs(knowledge): add entry template with gotcha/pattern/decision guidelines"
```
