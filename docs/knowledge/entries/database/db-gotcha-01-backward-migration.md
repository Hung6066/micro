---
id: db-gotcha-01
type: gotcha
domain: database
tags: [migration, cockroachdb, backward-compat, dotnet]
severity: critical
agent: @dba
author: @architect
date: 2026-07-17
related: []
---

# Migration phải luôn backward-compatible

## Vấn đề
Migration chạy trước khi code mới deploy. Nếu migration không backward-compatible (DROP COLUMN, RENAME, thay đổi type), code cũ đang chạy sẽ fail.

## Hậu quả
- Service đang chạy throw exception khi migration chạy
- Rollback phức tạp, có thể mất dữ liệu
- Downtime không mong muốn

## Cách làm đúng
```sql
-- ✅ ĐÚNG: ADD COLUMN với DEFAULT — code cũ vẫn chạy bình thường
ALTER TABLE patientdb.Patients ADD COLUMN PreferredLanguage STRING(10) DEFAULT 'vi';

-- ✅ ĐÚNG: Quy trình an toàn cho DROP COLUMN (3 bước, 3 lần deploy)
-- Deploy 1: Đánh dấu deprecated, code ngừng đọc cột
-- Deploy 2: Migration DROP COLUMN
-- Deploy 3: Dọn code tham chiếu cũ

-- ❌ SAI: DROP COLUMN ngay — code cũ sẽ fail
ALTER TABLE patientdb.Patients DROP COLUMN Phone;

-- ❌ SAI: RENAME COLUMN — code cũ không biết tên mới
ALTER TABLE patientdb.Patients RENAME COLUMN Phone TO ContactPhone;
```

## Quy tắc an toàn
| Hành động | An toàn? | Điều kiện |
|---|---|---|
| `ADD COLUMN` | ✅ | Có DEFAULT, nullable hoặc có default value |
| `ADD INDEX` | ✅ | Luôn an toàn |
| `DROP COLUMN` | ❌ | Cần 3-step deploy |
| `RENAME COLUMN` | ❌ | Không bao giờ an toàn |
| `ALTER TYPE` | ❌ | Cần tạo cột mới, migrate data, drop cột cũ |

## Đã xảy ra ở đâu
- IdentityService: migration 0023 DROP COLUMN gây 500 error trong 3 phút (tháng 4/2026)
