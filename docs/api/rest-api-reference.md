# His.Hope REST API Reference

## Tổng quan

His.Hope là hệ thống quản lý bệnh viện (Hospital Information System - HIS) được xây dựng theo kiến trúc microservices. Mỗi service expose REST API qua HTTPS với JWT Bearer authentication.

### Base URLs

| Service | HTTPS Port | HTTP Port | Base Path |
|---|---|---|---|
| IdentityService | 5001 | 5012 | `/api/v1` |
| PatientService | 5002 | 5008 | `/api/v1` |
| AppointmentService | 5003 | 5009 | `/api/v1` |
| ClinicalService | 5004 | 5010 | `/api/v1` |
| LabService | 5017 | 5018 | `/api/v1` |
| BillingService | 5021 | 5022 | `/api/v1` |
| PharmacyService | 5011 | 5012 | `/api/v1` |

### Authentication

Tất cả các endpoint (trừ login, register, refresh, health check) yêu cầu JWT Bearer token trong Authorization header:

```
Authorization: Bearer <access_token>
```

### Pagination

Các endpoint danh sách hỗ trợ pagination với query parameters:

```
?page=1&pageSize=20
```

Response paginated trả về:

```json
{
  "items": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20
}
```

### Error Response Format

Tất cả lỗi trả về theo RFC 7807 Problem Details format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Không tìm thấy bệnh nhân với ID: 550e8400-e29b-41d4-a716-446655440000",
  "traceId": "0HMPE3RSUV5JS"
}
```

### Correlation ID

Gửi `X-Correlation-ID` header để trace request qua các service:

```
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000
```

---

## IdentityService (Port 5001/5012)

Quản lý authentication, users, roles, permissions, settings và audit logs.

### Authentication Endpoints

#### POST /api/v1/auth/login

Đăng nhập và nhận JWT token pair.

- **Auth**: Không yêu cầu
- **Permission**: Không

**Request Body**:

```json
{
  "username": "bs.nguyen",
  "password": "SecureP@ss1",
  "deviceInfo": "Mozilla/5.0 Chrome/120",
  "ipAddress": "192.168.1.100"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| username | string | Yes | Tên đăng nhập |
| password | string | Yes | Mật khẩu |
| deviceInfo | string | No | Thông tin thiết bị cho audit log |
| ipAddress | string | No | Địa chỉ IP cho audit log |

**Response** (200 OK):

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl",
  "expiresAt": "2026-07-16T12:00:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "bs.nguyen",
    "email": "bs.nguyen@benhvienx.vn",
    "firstName": "Văn",
    "lastName": "Nguyễn",
    "middleName": "Anh",
    "fullName": "Nguyễn Văn Anh",
    "licenseNumber": "MD-2020-04521",
    "specialty": "Nội tổng quát",
    "roles": ["Provider"]
  }
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Đăng nhập thành công |
| 401 | Sai username hoặc password |

**cURL Example**:

```bash
curl -k -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"bs.nguyen","password":"SecureP@ss1"}'
```

---

#### POST /api/v1/auth/register

Đăng ký tài khoản mới.

- **Auth**: Không yêu cầu
- **Permission**: Không

**Request Body**:

```json
{
  "username": "bs.lethimai",
  "email": "bs.mai@benhvienx.vn",
  "password": "SecureP@ss2",
  "firstName": "Thị",
  "lastName": "Lê",
  "middleName": "Mai",
  "licenseNumber": "MD-2019-03210",
  "specialty": "Nhi khoa",
  "deviceInfo": "PostmanRuntime/7.32",
  "ipAddress": "10.0.0.50"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| username | string | Yes | Tên đăng nhập duy nhất |
| email | string | Yes | Email duy nhất |
| password | string | Yes | Mật khẩu (ít nhất 8 ký tự, có chữ, số, ký tự đặc biệt) |
| firstName | string | Yes | Tên |
| lastName | string | Yes | Họ |
| middleName | string | No | Tên đệm |
| licenseNumber | string | No | Số giấy phép hành nghề |
| specialty | string | No | Chuyên khoa |
| deviceInfo | string | No | Thông tin thiết bị |
| ipAddress | string | No | Địa chỉ IP |

**Response** (201 Created):

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "YW5vdGhlciByZWZyZXNoIHRva2Vu",
  "expiresAt": "2026-07-16T12:00:00Z",
  "user": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "username": "bs.lethimai",
    "email": "bs.mai@benhvienx.vn",
    "firstName": "Thị",
    "lastName": "Lê",
    "middleName": "Mai",
    "fullName": "Lê Thị Mai",
    "licenseNumber": "MD-2019-03210",
    "specialty": "Nhi khoa",
    "roles": ["Provider"]
  }
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 201 | Đăng ký thành công |
| 400 | Dữ liệu không hợp lệ (username/email đã tồn tại) |

---

#### POST /api/v1/auth/refresh

Làm mới access token bằng refresh token.

- **Auth**: Không yêu cầu
- **Permission**: Không

**Request Body**:

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl",
  "deviceInfo": "Mozilla/5.0 Chrome/120",
  "ipAddress": "192.168.1.100"
}
```

**Response** (200 OK):

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4=",
  "expiresAt": "2026-07-16T13:00:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "username": "bs.nguyen",
    "email": "bs.nguyen@benhvienx.vn",
    "firstName": "Văn",
    "lastName": "Nguyễn",
    "fullName": "Nguyễn Văn Anh",
    "roles": ["Provider"]
  }
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Refresh thành công |
| 401 | Refresh token hết hạn hoặc không hợp lệ |

---

#### POST /api/v1/auth/logout

Thu hồi refresh token. Yêu cầu authentication.

- **Auth**: Bearer JWT
- **Permission**: Không (chỉ cần authenticated)

**Request Body**:

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJl"
}
```

**Response** (204 No Content) - Không có body.

---

#### POST /api/v1/auth/revoke

Thu hồi tất cả refresh token của một user (admin only).

- **Auth**: Bearer JWT
- **Permission**: `admin.users.write`

**Request Body**:

```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response** (204 No Content).

---

### User Management Endpoints

#### GET /api/v1/auth/users

Lấy danh sách users (phân trang + filter).

- **Auth**: Bearer JWT
- **Permission**: `admin.users.read`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| search | string | null | Tìm theo tên, username, email |
| role | string | null | Lọc theo role |
| isActive | bool | null | Lọc trạng thái active |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userName": "bs.nguyen",
      "email": "bs.nguyen@benhvienx.vn",
      "phoneNumber": "0987654321",
      "firstName": "Văn",
      "lastName": "Nguyễn",
      "middleName": "Anh",
      "fullName": "Nguyễn Văn Anh",
      "licenseNumber": "MD-2020-04521",
      "specialty": "Nội tổng quát",
      "isActive": true,
      "createdAt": "2025-01-15T08:30:00Z",
      "lastLoginAt": "2026-07-16T07:45:00Z",
      "roles": ["Provider"]
    }
  ],
  "totalCount": 45,
  "page": 1,
  "pageSize": 20
}
```

**cURL Example**:

```bash
curl -k -X GET "https://localhost:5001/api/v1/auth/users?page=1&pageSize=10&search=nguyen&isActive=true" \
  -H "Authorization: Bearer $TOKEN"
```

---

#### GET /api/v1/auth/users/{id}

Lấy chi tiết một user.

- **Auth**: Bearer JWT
- **Permission**: `admin.users.read`

**Path Parameters**:

| Param | Type | Description |
|---|---|---|
| id | GUID | User ID |

**Response** (200 OK):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userName": "bs.nguyen",
  "email": "bs.nguyen@benhvienx.vn",
  "phoneNumber": "0987654321",
  "firstName": "Văn",
  "lastName": "Nguyễn",
  "middleName": "Anh",
  "fullName": "Nguyễn Văn Anh",
  "licenseNumber": "MD-2020-04521",
  "specialty": "Nội tổng quát",
  "isActive": true,
  "createdAt": "2025-01-15T08:30:00Z",
  "lastLoginAt": "2026-07-16T07:45:00Z",
  "roles": ["Provider"]
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy user |

---

#### POST /api/v1/auth/users

Tạo user mới.

- **Auth**: Bearer JWT
- **Permission**: `admin.users.write`

**Request Body**:

```json
{
  "username": "dd.tranthibich",
  "email": "dd.bich@benhvienx.vn",
  "password": "Str0ngP@ss!",
  "firstName": "Thị",
  "lastName": "Trần",
  "middleName": "Bích",
  "licenseNumber": "RN-2021-00345",
  "specialty": "Điều dưỡng CKI",
  "phoneNumber": "0912345678",
  "role": "Nurse"
}
```

**Response** (201 Created):

```json
{
  "id": "c0a80121-7f6e-4f1a-b334-8d91f24b5e12",
  "userName": "dd.tranthibich",
  "email": "dd.bich@benhvienx.vn",
  "phoneNumber": "0912345678",
  "firstName": "Thị",
  "lastName": "Trần",
  "middleName": "Bích",
  "fullName": "Trần Thị Bích",
  "licenseNumber": "RN-2021-00345",
  "specialty": "Điều dưỡng CKI",
  "isActive": true,
  "createdAt": "2026-07-16T08:00:00Z",
  "roles": ["Nurse"]
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 201 | Tạo thành công |
| 400 | Username hoặc email đã tồn tại |

---

#### PUT /api/v1/auth/users/{id}

Cập nhật thông tin user.

- **Auth**: Bearer JWT
- **Permission**: `admin.users.write`

**Request Body**:

```json
{
  "firstName": "Văn",
  "lastName": "Nguyễn",
  "email": "bs.nguyen@benhvienx.vn",
  "phoneNumber": "0911223344",
  "role": "Provider",
  "isActive": true
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| firstName | string | No | Tên mới |
| lastName | string | No | Họ mới |
| email | string | No | Email mới |
| phoneNumber | string | No | Số điện thoại mới |
| role | string | No | Role mới |
| isActive | bool | No | Trạng thái kích hoạt |

**Response** (200 OK): Trả về `UserDetailDto` đã cập nhật.

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Cập nhật thành công |
| 400 | Dữ liệu không hợp lệ |
| 404 | Không tìm thấy user |

---

#### PUT /api/v1/auth/users/{id}/deactivate

Vô hiệu hóa user (soft delete).

- **Auth**: Bearer JWT
- **Permission**: `admin.users.write`

**Response** (204 No Content).

---

#### PUT /api/v1/auth/users/{id}/activate

Kích hoạt lại user đã bị vô hiệu hóa.

- **Auth**: Bearer JWT
- **Permission**: `admin.users.write`

**Response** (204 No Content).

---

#### PUT /api/v1/auth/users/{id}/roles

Gán roles cho user.

- **Auth**: Bearer JWT
- **Permission**: `admin.roles.write`

**Request Body**:

```json
{
  "roleIds": [
    "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "b2c3d4e5-f6a7-8901-bcde-f12345678901"
  ]
}
```

**Response** (200 OK): Trả về `UserDetailDto` với roles đã cập nhật.

---

### Role Management Endpoints

#### GET /api/v1/auth/roles

Lấy danh sách tất cả roles.

- **Auth**: Bearer JWT
- **Permission**: `admin.roles.read`

**Response** (200 OK):

```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Provider",
    "description": "Bác sĩ điều trị",
    "isSystem": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "permissions": [
      {"code": "patients.view", "name": "Xem bệnh nhân", "group": "patients", "description": "Xem danh sách và chi tiết bệnh nhân", "isSystem": true},
      {"code": "patients.create", "name": "Tạo bệnh nhân", "group": "patients", "description": "Đăng ký bệnh nhân mới", "isSystem": true},
      {"code": "clinical.view", "name": "Xem lâm sàng", "group": "clinical", "description": "Xem hồ sơ lâm sàng", "isSystem": true},
      {"code": "clinical.create", "name": "Tạo khám lâm sàng", "group": "clinical", "description": "Bắt đầu lượt khám mới", "isSystem": true},
      {"code": "clinical.update", "name": "Cập nhật lâm sàng", "group": "clinical", "description": "Ghi nhận sinh hiệu, chẩn đoán", "isSystem": true},
      {"code": "clinical.sign", "name": "Ký hoàn thành", "group": "clinical", "description": "Hoàn tất lượt khám", "isSystem": true},
      {"code": "appointments.view", "name": "Xem lịch hẹn", "group": "appointments", "description": "Xem danh sách lịch hẹn", "isSystem": true},
      {"code": "lab.create", "name": "Tạo xét nghiệm", "group": "lab", "description": "Chỉ định xét nghiệm", "isSystem": true},
      {"code": "lab.view", "name": "Xem xét nghiệm", "group": "lab", "description": "Xem kết quả xét nghiệm", "isSystem": true},
      {"code": "pharmacy.create", "name": "Kê đơn", "group": "pharmacy", "description": "Kê đơn thuốc", "isSystem": true},
      {"code": "pharmacy.view", "name": "Xem thuốc/đơn", "group": "pharmacy", "description": "Xem danh mục thuốc và đơn thuốc", "isSystem": true}
    ]
  }
]
```

---

#### POST /api/v1/auth/roles

Tạo role mới.

- **Auth**: Bearer JWT
- **Permission**: `admin.roles.write`

**Request Body**:

```json
{
  "name": "HeadNurse",
  "description": "Điều dưỡng trưởng khoa",
  "permissions": [
    "patients.view",
    "patients.create",
    "patients.update",
    "appointments.view",
    "appointments.check-in",
    "clinical.view",
    "clinical.update",
    "lab.view"
  ]
}
```

**Response** (201 Created):

```json
{
  "id": "d4e5f6a7-b8c9-0123-def0-123456789abc",
  "name": "HeadNurse",
  "description": "Điều dưỡng trưởng khoa",
  "isSystem": false,
  "createdAt": "2026-07-16T08:30:00Z",
  "permissions": [...]
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 201 | Tạo thành công |
| 400 | Role name đã tồn tại |

---

#### PUT /api/v1/auth/roles/{id}

Cập nhật role.

- **Auth**: Bearer JWT
- **Permission**: `admin.roles.write`

**Request Body**:

```json
{
  "name": "HeadNurse",
  "description": "Điều dưỡng trưởng - cập nhật quyền hạn",
  "permissions": [
    "patients.view",
    "patients.create",
    "patients.update",
    "patients.delete",
    "appointments.view",
    "appointments.check-in",
    "clinical.view",
    "clinical.update",
    "lab.view"
  ]
}
```

**Response** (200 OK): Trả về `RoleDto` đã cập nhật.

---

#### DELETE /api/v1/auth/roles/{id}

Xóa role (chỉ khi không có user nào được gán).

- **Auth**: Bearer JWT
- **Permission**: `admin.roles.write`

**Response** (204 No Content).

---

### Permission Endpoints

#### GET /api/v1/auth/permissions

Lấy danh sách tất cả permissions trong hệ thống.

- **Auth**: Bearer JWT
- **Permission**: `admin.permissions.read`

**Response** (200 OK):

```json
[
  {"code": "patients.view", "name": "Xem bệnh nhân", "group": "patients", "description": "Xem danh sách và chi tiết bệnh nhân", "isSystem": true},
  {"code": "patients.create", "name": "Tạo bệnh nhân", "group": "patients", "description": "Đăng ký bệnh nhân mới", "isSystem": true},
  {"code": "patients.update", "name": "Cập nhật bệnh nhân", "group": "patients", "description": "Cập nhật thông tin bệnh nhân", "isSystem": true},
  {"code": "patients.delete", "name": "Vô hiệu hóa bệnh nhân", "group": "patients", "description": "Vô hiệu hóa hồ sơ bệnh nhân", "isSystem": true},
  {"code": "appointments.view", "name": "Xem lịch hẹn", "group": "appointments", "description": "Xem danh sách lịch hẹn", "isSystem": true},
  {"code": "appointments.create", "name": "Tạo lịch hẹn", "group": "appointments", "description": "Đặt lịch hẹn mới", "isSystem": true},
  {"code": "appointments.cancel", "name": "Hủy lịch hẹn", "group": "appointments", "description": "Hủy lịch hẹn đã đặt", "isSystem": true},
  {"code": "appointments.check-in", "name": "Check-in", "group": "appointments", "description": "Xác nhận bệnh nhân đến khám", "isSystem": true},
  {"code": "appointments.update", "name": "Cập nhật lịch hẹn", "group": "appointments", "description": "Cập nhật và check-out lịch hẹn", "isSystem": true},
  {"code": "clinical.view", "name": "Xem lâm sàng", "group": "clinical", "description": "Xem hồ sơ lâm sàng", "isSystem": true},
  {"code": "clinical.create", "name": "Tạo khám lâm sàng", "group": "clinical", "description": "Bắt đầu lượt khám mới", "isSystem": true},
  {"code": "clinical.update", "name": "Cập nhật lâm sàng", "group": "clinical", "description": "Ghi nhận sinh hiệu, chẩn đoán", "isSystem": true},
  {"code": "clinical.sign", "name": "Ký hoàn thành", "group": "clinical", "description": "Hoàn tất và ký lượt khám", "isSystem": true},
  {"code": "lab.view", "name": "Xem xét nghiệm", "group": "lab", "description": "Xem danh sách và kết quả xét nghiệm", "isSystem": true},
  {"code": "lab.create", "name": "Tạo xét nghiệm", "group": "lab", "description": "Chỉ định xét nghiệm mới", "isSystem": true},
  {"code": "lab.update", "name": "Cập nhật xét nghiệm", "group": "lab", "description": "Submit và thu thập mẫu xét nghiệm", "isSystem": true},
  {"code": "lab.result", "name": "Nhập kết quả", "group": "lab", "description": "Nhập kết quả xét nghiệm", "isSystem": true},
  {"code": "lab.cancel", "name": "Hủy xét nghiệm", "group": "lab", "description": "Hủy chỉ định xét nghiệm", "isSystem": true},
  {"code": "billing.view", "name": "Xem hóa đơn", "group": "billing", "description": "Xem danh sách và chi tiết hóa đơn", "isSystem": true},
  {"code": "billing.create", "name": "Tạo hóa đơn", "group": "billing", "description": "Tạo hóa đơn mới", "isSystem": true},
  {"code": "billing.pay", "name": "Thanh toán", "group": "billing", "description": "Ghi nhận thanh toán hóa đơn", "isSystem": true},
  {"code": "billing.void", "name": "Hủy hóa đơn", "group": "billing", "description": "Hủy hóa đơn đã tạo", "isSystem": true},
  {"code": "pharmacy.view", "name": "Xem thuốc/đơn", "group": "pharmacy", "description": "Xem danh mục thuốc và đơn thuốc", "isSystem": true},
  {"code": "pharmacy.create", "name": "Kê đơn / Thêm thuốc", "group": "pharmacy", "description": "Kê đơn thuốc và thêm thuốc vào danh mục", "isSystem": true},
  {"code": "pharmacy.update", "name": "Cập nhật thuốc", "group": "pharmacy", "description": "Cập nhật thông tin thuốc", "isSystem": true},
  {"code": "pharmacy.dispense", "name": "Xuất thuốc", "group": "pharmacy", "description": "Xuất thuốc theo đơn (fill prescription)", "isSystem": true},
  {"code": "pharmacy.cancel", "name": "Hủy đơn thuốc", "group": "pharmacy", "description": "Hủy đơn thuốc đã kê", "isSystem": true},
  {"code": "reports.view", "name": "Xem báo cáo", "group": "reports", "description": "Xem dashboard và báo cáo thống kê", "isSystem": true},
  {"code": "admin.users.read", "name": "Xem users", "group": "admin", "description": "Xem danh sách người dùng hệ thống", "isSystem": true},
  {"code": "admin.users.write", "name": "Quản lý users", "group": "admin", "description": "Tạo, sửa, vô hiệu hóa người dùng", "isSystem": true},
  {"code": "admin.roles.read", "name": "Xem roles", "group": "admin", "description": "Xem danh sách roles", "isSystem": true},
  {"code": "admin.roles.write", "name": "Quản lý roles", "group": "admin", "description": "Tạo, sửa, xóa roles", "isSystem": true},
  {"code": "admin.permissions.read", "name": "Xem permissions", "group": "admin", "description": "Xem danh sách permissions", "isSystem": true},
  {"code": "admin.settings.read", "name": "Xem cài đặt", "group": "admin", "description": "Xem cấu hình hệ thống", "isSystem": true},
  {"code": "admin.settings.write", "name": "Sửa cài đặt", "group": "admin", "description": "Cập nhật cấu hình hệ thống", "isSystem": true},
  {"code": "admin.audit.read", "name": "Xem audit log", "group": "admin", "description": "Xem nhật ký kiểm toán", "isSystem": true}
]
```

---

### Settings Endpoints

#### GET /api/v1/settings

Lấy tất cả system settings.

- **Auth**: Bearer JWT
- **Permission**: `admin.settings.read`

**Response** (200 OK):

```json
[
  {
    "key": "hospital.name",
    "value": "Bệnh viện Đa khoa X",
    "description": "Tên bệnh viện hiển thị trên header",
    "category": "general",
    "updatedAt": "2026-06-15T10:30:00Z",
    "updatedBy": "admin"
  },
  {
    "key": "appointment.defaultDuration",
    "value": "30",
    "description": "Thời lượng mặc định một lượt hẹn (phút)",
    "category": "appointment",
    "updatedAt": "2026-05-20T14:00:00Z",
    "updatedBy": "admin"
  },
  {
    "key": "billing.taxRate",
    "value": "8.0",
    "description": "Thuế suất VAT mặc định (%)",
    "category": "billing",
    "updatedAt": "2026-04-10T09:00:00Z",
    "updatedBy": "admin"
  }
]
```

---

#### GET /api/v1/settings/{key}

Lấy một setting theo key.

- **Auth**: Bearer JWT
- **Permission**: `admin.settings.read`

**Response** (200 OK):

```json
{
  "key": "hospital.name",
  "value": "Bệnh viện Đa khoa X",
  "description": "Tên bệnh viện hiển thị trên header",
  "category": "general",
  "updatedAt": "2026-06-15T10:30:00Z",
  "updatedBy": "admin"
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy setting |

---

#### PUT /api/v1/settings/{key}

Cập nhật một setting.

- **Auth**: Bearer JWT
- **Permission**: `admin.settings.write`

**Request Body**:

```json
{
  "value": "Bệnh viện Đa khoa X - Cơ sở 1",
  "description": "Tên bệnh viện hiển thị trên header - cập nhật 07/2026"
}
```

**Response** (200 OK):

```json
{
  "key": "hospital.name",
  "value": "Bệnh viện Đa khoa X - Cơ sở 1",
  "description": "Tên bệnh viện hiển thị trên header - cập nhật 07/2026",
  "category": "general",
  "updatedAt": "2026-07-16T09:00:00Z",
  "updatedBy": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

#### PUT /api/v1/settings

Cập nhật nhiều settings cùng lúc (bulk update).

- **Auth**: Bearer JWT
- **Permission**: `admin.settings.write`

**Request Body**:

```json
[
  {"key": "hospital.name", "value": "Bệnh viện Đa khoa X - Cơ sở 1"},
  {"key": "appointment.defaultDuration", "value": "45"},
  {"key": "billing.taxRate", "value": "10.0"}
]
```

**Response** (200 OK): Mảng các `SystemSettingDto` đã cập nhật.

---

### Audit Log Endpoints

#### GET /api/v1/audit-logs

Lấy danh sách audit logs (phân trang + filter).

- **Auth**: Bearer JWT
- **Permission**: `admin.audit.read`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| userId | string | null | Lọc theo user ID |
| action | string | null | Lọc theo action (CREATE, READ, UPDATE, DELETE) |
| resourceType | string | null | Lọc theo loại resource (Patient, Encounter, ...) |
| resourceId | string | null | Lọc theo resource ID |
| dateFrom | DateTime | null | Từ ngày |
| dateTo | DateTime | null | Đến ngày |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "ffa1b2c3-d4e5-6789-abcd-ef0123456789",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userName": "bs.nguyen",
      "action": "READ",
      "resourceType": "Patient",
      "resourceId": "550e8400-e29b-41d4-a716-446655440000",
      "details": "Xem hồ sơ bệnh nhân Nguyễn Thị Hương",
      "ipAddress": "192.168.1.100",
      "userAgent": "Mozilla/5.0 Chrome/120",
      "timestamp": "2026-07-16T08:15:30Z"
    }
  ],
  "totalCount": 1250,
  "page": 1,
  "pageSize": 20
}
```

---

#### GET /api/v1/audit-logs/{id}

Lấy chi tiết một audit log entry.

- **Auth**: Bearer JWT
- **Permission**: `admin.audit.read`

**Response** (200 OK): `AuditLogDto` (xem schema ở trên).

---

#### GET /api/v1/auth/me

Lấy thông tin user hiện tại từ JWT token.

- **Auth**: Bearer JWT
- **Permission**: Không (chỉ cần authenticated)

**Response** (200 OK): `UserDto` (xem schema login response).

---

## PatientService (Port 5002/5008)

Quản lý hồ sơ bệnh nhân (Patient Registry).

### GET /api/v1/patients/search

Tìm kiếm bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `patients.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| q | string | "" | Từ khóa tìm kiếm (tên, SĐT, mã BHYT) |
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "fullName": "Nguyễn Thị Hương",
      "firstName": "Thị",
      "lastName": "Nguyễn",
      "middleName": "Hương",
      "dateOfBirth": "1985-03-15T00:00:00Z",
      "age": 41,
      "genderCode": "F",
      "genderName": "Nữ",
      "phone": "0987654321",
      "email": "huong.nguyen@gmail.com",
      "street": "123 Lê Lợi",
      "district": "Quận 1",
      "city": "TP. Hồ Chí Minh",
      "province": "TP. Hồ Chí Minh",
      "postalCode": "700000",
      "country": "Việt Nam",
      "bloodTypeCode": "B",
      "bloodTypeName": "B",
      "insuranceId": "HS0123456789",
      "nationalId": "079185012345",
      "isActive": true,
      "createdAt": "2025-06-10T08:00:00Z",
      "updatedAt": "2026-03-20T14:30:00Z",
      "allergies": [
        {
          "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "allergen": "Penicillin",
          "reaction": "Phát ban, khó thở",
          "severity": "Nặng",
          "recordedDate": "2025-06-10T08:00:00Z",
          "isActive": true
        }
      ],
      "conditions": [
        {
          "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
          "conditionName": "Tăng huyết áp vô căn",
          "icd10Code": "I10",
          "onsetDate": "2020-01-01T00:00:00Z",
          "isChronic": true,
          "isActive": true
        }
      ]
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

**cURL Example**:

```bash
curl -k -X GET "https://localhost:5002/api/v1/patients/search?q=Hương&page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

---

### GET /api/v1/patients/{id}

Lấy chi tiết hồ sơ bệnh nhân. Có Redis caching (TTL 5 phút).

- **Auth**: Bearer JWT
- **Permission**: `patients.view`

**Response** (200 OK): `PatientDto` (xem schema ở trên).

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy bệnh nhân |

---

### POST /api/v1/patients

Đăng ký bệnh nhân mới.

- **Auth**: Bearer JWT
- **Permission**: `patients.create`

**Request Body**:

```json
{
  "firstName": "Văn",
  "lastName": "Trần",
  "middleName": "Hùng",
  "dateOfBirth": "1990-07-20T00:00:00Z",
  "genderCode": "M",
  "phone": "0911222333",
  "email": "hung.tran@gmail.com",
  "street": "456 Nguyễn Huệ",
  "district": "Quận Hải Châu",
  "city": "Đà Nẵng",
  "province": "Đà Nẵng",
  "postalCode": "550000",
  "country": "Việt Nam",
  "insuranceId": "HS0987654321",
  "nationalId": "079190012345"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| firstName | string | Yes | Tên |
| lastName | string | Yes | Họ |
| middleName | string | No | Tên đệm |
| dateOfBirth | DateTime | Yes | Ngày sinh |
| genderCode | string | Yes | Mã giới tính (M/F/O) |
| phone | string | Yes | Số điện thoại |
| email | string | No | Email |
| street | string | Yes | Số nhà, đường |
| district | string | Yes | Quận/Huyện |
| city | string | Yes | Thành phố |
| province | string | Yes | Tỉnh/Thành phố |
| postalCode | string | No | Mã bưu điện |
| country | string | Yes | Quốc gia |
| insuranceId | string | No | Mã BHYT |
| nationalId | string | No | CMND/CCCD |

**Response** (201 Created):

```json
{
  "id": "660e8400-e29b-41d4-a716-446655440001",
  "fullName": "Trần Văn Hùng",
  "firstName": "Văn",
  "lastName": "Trần",
  "middleName": "Hùng",
  "dateOfBirth": "1990-07-20T00:00:00Z",
  "age": 35,
  "genderCode": "M",
  "genderName": "Nam",
  "phone": "0911222333",
  "email": "hung.tran@gmail.com",
  "street": "456 Nguyễn Huệ",
  "district": "Quận Hải Châu",
  "city": "Đà Nẵng",
  "province": "Đà Nẵng",
  "postalCode": "550000",
  "country": "Việt Nam",
  "insuranceId": "HS0987654321",
  "nationalId": "079190012345",
  "isActive": true,
  "createdAt": "2026-07-16T09:00:00Z",
  "allergies": [],
  "conditions": []
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 201 | Tạo thành công |
| 400 | Dữ liệu không hợp lệ |

---

### PUT /api/v1/patients/{id}

Cập nhật thông tin bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `patients.update`

**Request Body**:

```json
{
  "firstName": "Văn",
  "lastName": "Trần",
  "middleName": "Hùng",
  "dateOfBirth": "1990-07-20T00:00:00Z",
  "genderCode": "M",
  "phone": "0911222333",
  "email": "hung.tran@newemail.com",
  "street": "789 Trần Phú",
  "district": "Quận Hải Châu",
  "city": "Đà Nẵng",
  "province": "Đà Nẵng",
  "postalCode": "550000",
  "country": "Việt Nam"
}
```

**Response** (200 OK): `PatientDto` đã cập nhật.

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Cập nhật thành công |
| 404 | Không tìm thấy bệnh nhân |

---

### PATCH /api/v1/patients/{id}/deactivate

Vô hiệu hóa hồ sơ bệnh nhân (soft delete).

- **Auth**: Bearer JWT
- **Permission**: `patients.delete`

**Response** (204 No Content).

---

### PATCH /api/v1/patients/{id}/reactivate

Kích hoạt lại hồ sơ bệnh nhân đã bị vô hiệu hóa.

- **Auth**: Bearer JWT
- **Permission**: `patients.update`

**Response** (204 No Content).

---

## AppointmentService (Port 5003/5009)

Quản lý lịch hẹn khám bệnh.

### GET /api/v1/appointments

Lấy danh sách tất cả lịch hẹn (với caching TTL 5 phút).

- **Auth**: Bearer JWT
- **Permission**: `appointments.view`

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "770e8400-e29b-41d4-a716-446655440002",
      "patientId": "550e8400-e29b-41d4-a716-446655440000",
      "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "scheduledDate": "2026-07-17T00:00:00Z",
      "startTime": "08:00:00",
      "endTime": "08:30:00",
      "statusCode": "SCHEDULED",
      "statusName": "Đã lên lịch",
      "typeCode": "KHAM_MOI",
      "typeName": "Khám mới",
      "reason": "Đau đầu kéo dài, chóng mặt",
      "location": "Phòng 205 - Tầng 2",
      "createdAt": "2026-07-16T09:00:00Z"
    }
  ],
  "totalCount": 25,
  "page": 1,
  "pageSize": 1000
}
```

---

### GET /api/v1/appointments/search

Tìm kiếm lịch hẹn.

- **Auth**: Bearer JWT
- **Permission**: `appointments.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| q | string | "" | Từ khóa tìm kiếm |
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |

**cURL Example**:

```bash
curl -k -X GET "https://localhost:5003/api/v1/appointments/search?q=KHAM_MOI&page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

---

### GET /api/v1/appointments/{id}

Lấy chi tiết một lịch hẹn.

- **Auth**: Bearer JWT
- **Permission**: `appointments.view`

**Response** (200 OK): `AppointmentDto` (xem schema GET /appointments).

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy lịch hẹn |

---

### GET /api/v1/appointments/patient/{patientId}

Lấy lịch sử lịch hẹn của một bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `appointments.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| fromDate | DateTime | null | Từ ngày |
| toDate | DateTime | null | Đến ngày |

**Response** (200 OK): Paged result của `AppointmentDto`.

---

### POST /api/v1/appointments

Đặt lịch hẹn mới. Tự động kiểm tra patient tồn tại qua gRPC call đến PatientService.

- **Auth**: Bearer JWT
- **Permission**: `appointments.create`

**Request Body**:

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "scheduledDate": "2026-07-18T00:00:00Z",
  "startTime": "14:00:00",
  "durationMinutes": 30,
  "typeCode": "TAI_KHAM",
  "reason": "Tái khám sau điều trị tăng huyết áp",
  "location": "Phòng 301 - Tầng 3"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| patientId | GUID | Yes | ID bệnh nhân |
| providerId | GUID | Yes | ID bác sĩ |
| scheduledDate | DateTime | Yes | Ngày hẹn |
| startTime | TimeSpan | Yes | Giờ bắt đầu |
| durationMinutes | int | Yes | Thời lượng (phút) |
| typeCode | string | Yes | Loại hẹn (KHAM_MOI, TAI_KHAM, ...) |
| reason | string | No | Lý do khám |
| location | string | No | Địa điểm/phòng khám |

**Response** (201 Created):

```json
{
  "id": "880e8400-e29b-41d4-a716-446655440003",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "scheduledDate": "2026-07-18T00:00:00Z",
  "startTime": "14:00:00",
  "endTime": "14:30:00",
  "statusCode": "SCHEDULED",
  "statusName": "Đã lên lịch",
  "typeCode": "TAI_KHAM",
  "typeName": "Tái khám",
  "reason": "Tái khám sau điều trị tăng huyết áp",
  "location": "Phòng 301 - Tầng 3",
  "createdAt": "2026-07-16T09:15:00Z"
}
```

**Status Codes**:
| Code | Description |
|---|---|
| 201 | Tạo thành công |
| 404 | Không tìm thấy bệnh nhân |

---

### PUT /api/v1/appointments/{id}/cancel

Hủy lịch hẹn.

- **Auth**: Bearer JWT
- **Permission**: `appointments.cancel`

**Request Body**:

```json
{
  "reason": "Bệnh nhân yêu cầu dời lịch"
}
```

**Response** (204 No Content).

---

### PUT /api/v1/appointments/{id}/checkin

Check-in bệnh nhân khi đến khám.

- **Auth**: Bearer JWT
- **Permission**: `appointments.check-in`

**Response** (204 No Content).

---

### PUT /api/v1/appointments/{id}/checkout

Check-out sau khi hoàn tất khám.

- **Auth**: Bearer JWT
- **Permission**: `appointments.update`

**Response** (204 No Content).

---

## ClinicalService (Port 5004/5010)

Quản lý lượt khám lâm sàng (encounters), sinh hiệu, chẩn đoán.

### GET /api/v1/encounters

Lấy danh sách tất cả lượt khám.

- **Auth**: Bearer JWT
- **Permission**: `clinical.view`

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "990e8400-e29b-41d4-a716-446655440004",
      "patientId": "550e8400-e29b-41d4-a716-446655440000",
      "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "appointmentId": "880e8400-e29b-41d4-a716-446655440003",
      "encounterDate": "2026-07-16T08:30:00Z",
      "encounterTypeCode": "KHAM_NGOAI_TRU",
      "encounterTypeName": "Khám ngoại trú",
      "statusCode": "IN_PROGRESS",
      "statusName": "Đang khám",
      "chiefComplaint": "Đau đầu, chóng mặt 3 ngày nay",
      "hasVitals": true,
      "diagnosisCount": 1,
      "createdAt": "2026-07-16T08:30:00Z"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 1000
}
```

---

### GET /api/v1/encounters/search

Tìm kiếm lượt khám.

- **Auth**: Bearer JWT
- **Permission**: `clinical.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| q | string | "" | Từ khóa tìm kiếm |
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |

---

### GET /api/v1/encounters/{id}

Lấy chi tiết lượt khám.

- **Auth**: Bearer JWT
- **Permission**: `clinical.view`

**Response** (200 OK): `EncounterDto` (xem schema GET /encounters).

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy lượt khám |

---

### GET /api/v1/encounters/patient/{patientId}

Lấy lịch sử khám của một bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `clinical.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| fromDate | DateTime | null | Từ ngày |
| toDate | DateTime | null | Đến ngày |

**Response** (200 OK): Paged result của `EncounterDto`.

---

### POST /api/v1/encounters

Bắt đầu lượt khám mới.

- **Auth**: Bearer JWT
- **Permission**: `clinical.create`

**Request Body**:

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "appointmentId": "880e8400-e29b-41d4-a716-446655440003",
  "encounterTypeCode": "KHAM_NGOAI_TRU"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| patientId | GUID | Yes | ID bệnh nhân |
| providerId | GUID | Yes | ID bác sĩ |
| appointmentId | GUID | No | ID lịch hẹn (nếu có) |
| encounterTypeCode | string | Yes | Loại khám |

**Response** (201 Created):

```json
{
  "id": "aa0e8400-e29b-41d4-a716-446655440005",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "appointmentId": "880e8400-e29b-41d4-a716-446655440003",
  "encounterDate": "2026-07-16T09:30:00Z",
  "encounterTypeCode": "KHAM_NGOAI_TRU",
  "encounterTypeName": "Khám ngoại trú",
  "statusCode": "IN_PROGRESS",
  "statusName": "Đang khám",
  "hasVitals": false,
  "diagnosisCount": 0,
  "createdAt": "2026-07-16T09:30:00Z"
}
```

---

### POST /api/v1/encounters/{id}/vitals

Ghi nhận sinh hiệu (vital signs) cho lượt khám.

- **Auth**: Bearer JWT
- **Permission**: `clinical.update`

**Request Body**:

```json
{
  "temperature": 37.5,
  "heartRate": 88,
  "respiratoryRate": 18,
  "systolicBP": 145,
  "diastolicBP": 92,
  "oxygenSaturation": 97.0,
  "heightCm": 165.0,
  "weightKg": 62.5,
  "bmi": 22.96
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| temperature | decimal | No | Nhiệt độ (°C) |
| heartRate | int | No | Nhịp tim (lần/phút) |
| respiratoryRate | int | No | Nhịp thở (lần/phút) |
| systolicBP | int | No | Huyết áp tâm thu (mmHg) |
| diastolicBP | int | No | Huyết áp tâm trương (mmHg) |
| oxygenSaturation | decimal | No | SpO2 (%) |
| heightCm | decimal | No | Chiều cao (cm) |
| weightKg | decimal | No | Cân nặng (kg) |
| bmi | decimal | No | BMI (tự tính hoặc để hệ thống tính) |

**Response** (200 OK): `EncounterDto` với `hasVitals: true`.

---

### POST /api/v1/encounters/{id}/diagnosis

Thêm chẩn đoán cho lượt khám.

- **Auth**: Bearer JWT
- **Permission**: `clinical.update`

**Request Body**:

```json
{
  "conditionName": "Tăng huyết áp vô căn chưa kiểm soát",
  "icd10Code": "I10",
  "isPrimary": true,
  "notes": "HA 145/92 mmHg, cần điều chỉnh thuốc"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| conditionName | string | Yes | Tên chẩn đoán |
| icd10Code | string | Yes | Mã ICD-10 |
| isPrimary | bool | Yes | Có phải chẩn đoán chính không |
| notes | string | No | Ghi chú chẩn đoán |

**Response** (200 OK): `EncounterDto` với chẩn đoán đã thêm.

---

### PUT /api/v1/encounters/{id}/complete

Hoàn tất lượt khám và ký (sign).

- **Auth**: Bearer JWT
- **Permission**: `clinical.update`

**Response** (204 No Content).

---

### GET /api/v1/dashboard/stats

Dashboard thống kê lượt khám.

- **Auth**: Bearer JWT
- **Permission**: `reports.view`

**Response** (200 OK):

```json
{
  "totalEncounters": 12450,
  "activeEncounters": 15,
  "todayEncounters": 42,
  "encountersByType": [
    {"type": "Khám ngoại trú", "code": "KHAM_NGOAI_TRU", "count": 10200},
    {"type": "Khám nội trú", "code": "KHAM_NOI_TRU", "count": 1800},
    {"type": "Cấp cứu", "code": "CAP_CUU", "count": 450}
  ],
  "recentEncounters": [
    {
      "id": "aa0e8400-e29b-41d4-a716-446655440005",
      "patientId": "550e8400-e29b-41d4-a716-446655440000",
      "encounterType": "Khám ngoại trú",
      "status": "Đang khám",
      "encounterDate": "2026-07-16T09:30:00Z",
      "createdAt": "2026-07-16T09:30:00Z"
    }
  ]
}
```

---

## LabService (Port 5017/5018)

Quản lý chỉ định và kết quả xét nghiệm.

### GET /api/v1/lab-orders

Lấy danh sách lab orders với filter.

- **Auth**: Bearer JWT
- **Permission**: `lab.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| search | string | null | Từ khóa tìm kiếm |
| patientId | GUID | null | Lọc theo bệnh nhân |
| status | string | null | Lọc theo trạng thái |
| dateFrom | DateTime | null | Từ ngày |
| dateTo | DateTime | null | Đến ngày |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "bb0e8400-e29b-41d4-a716-446655440006",
      "patientId": "550e8400-e29b-41d4-a716-446655440000",
      "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
      "orderDate": "2026-07-16T09:45:00Z",
      "statusCode": "PENDING",
      "statusName": "Chờ xử lý",
      "priorityCode": "ROUTINE",
      "priorityName": "Thường quy",
      "notes": "Kiểm tra định kỳ",
      "tests": [
        {
          "id": "cc0e8400-e29b-41d4-a716-446655440007",
          "testCode": "CBC",
          "testName": "Tổng phân tích tế bào máu",
          "specimenType": "Máu toàn phần",
          "statusCode": "PENDING",
          "statusName": "Chờ lấy mẫu",
          "orderedAt": "2026-07-16T09:45:00Z",
          "result": null
        },
        {
          "id": "dd0e8400-e29b-41d4-a716-446655440008",
          "testCode": "GLU",
          "testName": "Glucose máu lúc đói",
          "specimenType": "Huyết thanh",
          "statusCode": "PENDING",
          "statusName": "Chờ lấy mẫu",
          "orderedAt": "2026-07-16T09:45:00Z",
          "result": null
        }
      ]
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

---

### GET /api/v1/lab-orders/{id}

Lấy chi tiết lab order.

- **Auth**: Bearer JWT
- **Permission**: `lab.view`

**Response** (200 OK): `LabOrderDto` (xem schema ở trên).

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy lab order |

---

### GET /api/v1/lab-orders/patient/{patientId}

Lấy danh sách lab orders của một bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `lab.view`

**Response** (200 OK): Collection của `LabOrderDto`.

---

### POST /api/v1/lab-orders

Tạo chỉ định xét nghiệm mới.

- **Auth**: Bearer JWT
- **Permission**: `lab.create`

**Request Body**:

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "priorityCode": "ROUTINE",
  "notes": "Xét nghiệm máu định kỳ - theo dõi tăng huyết áp",
  "tests": [
    {
      "testCode": "CBC",
      "testName": "Tổng phân tích tế bào máu",
      "specimenType": "Máu toàn phần"
    },
    {
      "testCode": "GLU",
      "testName": "Glucose máu lúc đói",
      "specimenType": "Huyết thanh"
    },
    {
      "testCode": "LIPID",
      "testName": "Bilan lipid máu",
      "specimenType": "Huyết thanh"
    }
  ]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| patientId | GUID | Yes | ID bệnh nhân |
| providerId | GUID | Yes | ID bác sĩ chỉ định |
| encounterId | GUID | No | ID lượt khám liên quan |
| priorityCode | string | Yes | Mức ưu tiên (ROUTINE, STAT, URGENT) |
| notes | string | No | Ghi chú |
| tests | array | Yes | Danh sách xét nghiệm |
| tests[].testCode | string | Yes | Mã xét nghiệm |
| tests[].testName | string | Yes | Tên xét nghiệm |
| tests[].specimenType | string | No | Loại bệnh phẩm |

**Response** (201 Created):

```json
{
  "id": "bb0e8400-e29b-41d4-a716-446655440006",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "orderDate": "2026-07-16T09:45:00Z",
  "statusCode": "PENDING",
  "statusName": "Chờ xử lý",
  "priorityCode": "ROUTINE",
  "priorityName": "Thường quy",
  "notes": "Xét nghiệm máu định kỳ - theo dõi tăng huyết áp",
  "tests": [
    {
      "id": "cc0e8400-e29b-41d4-a716-446655440007",
      "testCode": "CBC",
      "testName": "Tổng phân tích tế bào máu",
      "specimenType": "Máu toàn phần",
      "statusCode": "PENDING",
      "statusName": "Chờ lấy mẫu",
      "orderedAt": "2026-07-16T09:45:00Z"
    },
    {
      "id": "dd0e8400-e29b-41d4-a716-446655440008",
      "testCode": "GLU",
      "testName": "Glucose máu lúc đói",
      "specimenType": "Huyết thanh",
      "statusCode": "PENDING",
      "statusName": "Chờ lấy mẫu",
      "orderedAt": "2026-07-16T09:45:00Z"
    },
    {
      "id": "ee0e8400-e29b-41d4-a716-446655440009",
      "testCode": "LIPID",
      "testName": "Bilan lipid máu",
      "specimenType": "Huyết thanh",
      "statusCode": "PENDING",
      "statusName": "Chờ lấy mẫu",
      "orderedAt": "2026-07-16T09:45:00Z"
    }
  ]
}
```

---

### PUT /api/v1/lab-orders/{id}/submit

Gửi lab order đến phòng xét nghiệm.

- **Auth**: Bearer JWT
- **Permission**: `lab.update`

**Response** (204 No Content).

---

### PUT /api/v1/lab-orders/{id}/collect

Thu thập mẫu bệnh phẩm cho tất cả tests trong order.

- **Auth**: Bearer JWT
- **Permission**: `lab.update`

**Response** (204 No Content).

---

### PUT /api/v1/lab-orders/{id}/result

Nhập kết quả xét nghiệm cho một test cụ thể.

- **Auth**: Bearer JWT
- **Permission**: `lab.result`

**Request Body**:

```json
{
  "testId": "cc0e8400-e29b-41d4-a716-446655440007",
  "value": "5.2",
  "abnormalFlagCode": "N",
  "notes": "Kết quả trong giới hạn bình thường"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| testId | GUID | Yes | ID của lab test |
| value | string | Yes | Giá trị kết quả |
| abnormalFlagCode | string | No | Cờ bất thường (N, H, L, A, C) |
| notes | string | No | Ghi chú kết quả |

**Response** (204 No Content).

---

### PUT /api/v1/lab-orders/{id}/cancel

Hủy lab order.

- **Auth**: Bearer JWT
- **Permission**: `lab.cancel`

**Request Body**:

```json
{
  "reason": "Bệnh nhân từ chối làm xét nghiệm"
}
```

**Response** (204 No Content).

---

## BillingService (Port 5021/5022)

Quản lý hóa đơn, thanh toán viện phí.

### GET /api/v1/invoices

Lấy danh sách hóa đơn với filter.

- **Auth**: Bearer JWT
- **Permission**: `billing.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| search | string | null | Từ khóa (invoice number, ...) |
| patientId | GUID | null | Lọc theo bệnh nhân |
| status | string | null | Lọc theo trạng thái |
| dateFrom | DateTime | null | Từ ngày |
| dateTo | DateTime | null | Đến ngày |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "ff0e8400-e29b-41d4-a716-446655440010",
      "patientId": "550e8400-e29b-41d4-a716-446655440000",
      "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
      "invoiceNumber": "INV-20260716-0001",
      "invoiceDate": "2026-07-16T10:00:00Z",
      "dueDate": "2026-07-23T00:00:00Z",
      "statusCode": "PENDING",
      "statusName": "Chờ thanh toán",
      "notes": "Hóa đơn khám ngoại trú",
      "subTotal": 850000.0,
      "taxAmount": 68000.0,
      "discountAmount": 50000.0,
      "totalAmount": 868000.0,
      "paidAmount": 0.0,
      "balanceDue": 868000.0,
      "createdAt": "2026-07-16T10:00:00Z",
      "lineItems": [
        {
          "id": "1110e840-e29b-41d4-a716-446655440011",
          "description": "Khám ngoại trú - Bác sĩ chuyên khoa",
          "quantity": 1,
          "unitPrice": 350000.0,
          "amount": 350000.0,
          "itemCode": "KHAM-CK",
          "itemTypeCode": "SERVICE",
          "itemTypeName": "Dịch vụ"
        },
        {
          "id": "2220e840-e29b-41d4-a716-446655440012",
          "description": "Tổng phân tích tế bào máu (CBC)",
          "quantity": 1,
          "unitPrice": 150000.0,
          "amount": 150000.0,
          "itemCode": "XN-CBC",
          "itemTypeCode": "LAB",
          "itemTypeName": "Xét nghiệm"
        }
      ],
      "payments": []
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

---

### GET /api/v1/invoices/{id}

Lấy chi tiết hóa đơn.

- **Auth**: Bearer JWT
- **Permission**: `billing.view`

**Response** (200 OK): `InvoiceDto` (xem schema ở trên).

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy hóa đơn |

---

### GET /api/v1/invoices/number/{invoiceNumber}

Lấy hóa đơn theo số hóa đơn.

- **Auth**: Bearer JWT
- **Permission**: `billing.view`

**Response** (200 OK): `InvoiceDto`.

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy hóa đơn |

---

### GET /api/v1/invoices/patient/{patientId}

Lấy danh sách hóa đơn của một bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `billing.view`

**Response** (200 OK): Collection của `InvoiceDto`.

---

### POST /api/v1/invoices

Tạo hóa đơn mới.

- **Auth**: Bearer JWT
- **Permission**: `billing.create`

**Request Body**:

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "invoiceNumber": "INV-20260716-0001",
  "invoiceDate": "2026-07-16T00:00:00Z",
  "dueDate": "2026-07-23T00:00:00Z",
  "notes": "Hóa đơn khám ngoại trú",
  "lineItems": [
    {
      "description": "Khám ngoại trú - Bác sĩ chuyên khoa",
      "quantity": 1,
      "unitPrice": 350000.0,
      "itemCode": "KHAM-CK",
      "itemTypeCode": "SERVICE"
    },
    {
      "description": "Tổng phân tích tế bào máu (CBC)",
      "quantity": 1,
      "unitPrice": 150000.0,
      "itemCode": "XN-CBC",
      "itemTypeCode": "LAB"
    },
    {
      "description": "Glucose máu lúc đói",
      "quantity": 1,
      "unitPrice": 120000.0,
      "itemCode": "XN-GLU",
      "itemTypeCode": "LAB"
    },
    {
      "description": "Bilan lipid máu",
      "quantity": 1,
      "unitPrice": 230000.0,
      "itemCode": "XN-LIPID",
      "itemTypeCode": "LAB"
    }
  ]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| patientId | GUID | Yes | ID bệnh nhân |
| encounterId | GUID | No | ID lượt khám liên quan |
| invoiceNumber | string | Yes | Số hóa đơn |
| invoiceDate | DateTime | Yes | Ngày tạo hóa đơn |
| dueDate | DateTime | No | Ngày đến hạn thanh toán |
| notes | string | No | Ghi chú |
| lineItems | array | Yes | Các mục trong hóa đơn |
| lineItems[].description | string | Yes | Mô tả dịch vụ/sản phẩm |
| lineItems[].quantity | int | Yes | Số lượng |
| lineItems[].unitPrice | decimal | Yes | Đơn giá (VNĐ) |
| lineItems[].itemCode | string | No | Mã dịch vụ/sản phẩm |
| lineItems[].itemTypeCode | string | No | Loại (SERVICE, LAB, DRUG, ...) |

**Response** (201 Created): `InvoiceDto` đã tạo.

---

### POST /api/v1/invoices/{id}/payments

Ghi nhận thanh toán cho hóa đơn. Nếu tổng thanh toán đủ totalAmount, tự động đổi status sang PAID.

- **Auth**: Bearer JWT
- **Permission**: `billing.pay`

**Request Body**:

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "amount": 868000.0,
  "paymentDate": "2026-07-16T11:00:00Z",
  "methodCode": "CASH",
  "referenceNumber": "PT-20260716-0001",
  "notes": "Thanh toán bằng tiền mặt"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| patientId | GUID | Yes | ID bệnh nhân |
| amount | decimal | Yes | Số tiền thanh toán (VNĐ) |
| paymentDate | DateTime | Yes | Ngày thanh toán |
| methodCode | string | Yes | Phương thức (CASH, BANK_TRANSFER, INSURANCE, VNPAY) |
| referenceNumber | string | No | Số tham chiếu giao dịch |
| notes | string | No | Ghi chú thanh toán |

**Response** (200 OK): `InvoiceDto` với payment đã thêm và `balanceDue` cập nhật.

---

### PUT /api/v1/invoices/{id}/void

Hủy hóa đơn (void - không xóa dữ liệu).

- **Auth**: Bearer JWT
- **Permission**: `billing.void`

**Request Body**:

```json
{
  "reason": "Sai thông tin bệnh nhân - tạo lại hóa đơn mới"
}
```

**Response** (204 No Content).

---

## PharmacyService (Port 5011/5012)

Quản lý danh mục thuốc và kê đơn.

### Medication Endpoints

#### GET /api/v1/medications

Lấy danh sách thuốc với filter và phân trang.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| search | string | null | Tìm theo tên thuốc, generic name |
| category | string | null | Lọc theo nhóm thuốc |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "3330e840-e29b-41d4-a716-446655440013",
      "name": "Amlodipine 5mg",
      "genericName": "Amlodipine besylate",
      "brandName": "Norvasc",
      "dosageForm": "Viên nén",
      "strength": "5mg",
      "route": "Uống",
      "requiresPrescription": true,
      "isActive": true,
      "category": "Thuốc tim mạch",
      "manufacturer": "Pfizer",
      "createdAt": "2025-01-10T00:00:00Z",
      "updatedAt": "2026-01-10T00:00:00Z"
    },
    {
      "id": "4440e840-e29b-41d4-a716-446655440014",
      "name": "Paracetamol 500mg",
      "genericName": "Paracetamol (Acetaminophen)",
      "brandName": "Panadol",
      "dosageForm": "Viên nén",
      "strength": "500mg",
      "route": "Uống",
      "requiresPrescription": false,
      "isActive": true,
      "category": "Thuốc giảm đau",
      "manufacturer": "GSK",
      "createdAt": "2025-01-10T00:00:00Z",
      "updatedAt": "2026-01-10T00:00:00Z"
    }
  ],
  "totalCount": 350,
  "page": 1,
  "pageSize": 20
}
```

---

#### GET /api/v1/medications/{id}

Lấy chi tiết một thuốc.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.view`

**Response** (200 OK): `MedicationDto` (xem schema ở trên).

**Status Codes**:
| Code | Description |
|---|---|
| 200 | Thành công |
| 404 | Không tìm thấy thuốc |

---

#### POST /api/v1/medications

Thêm thuốc mới vào danh mục.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.create`

**Request Body**:

```json
{
  "name": "Lisinopril 10mg",
  "genericName": "Lisinopril",
  "brandName": "Zestril",
  "dosageForm": "Viên nén",
  "strength": "10mg",
  "route": "Uống",
  "category": "Thuốc tim mạch",
  "manufacturer": "AstraZeneca",
  "requiresPrescription": true
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| name | string | Yes | Tên thuốc |
| genericName | string | No | Tên hoạt chất (generic) |
| brandName | string | No | Tên biệt dược |
| dosageForm | string | Yes | Dạng bào chế (viên nén, dung dịch, ...) |
| strength | string | Yes | Hàm lượng |
| route | string | No | Đường dùng (uống, tiêm, ...) |
| category | string | No | Nhóm thuốc |
| manufacturer | string | No | Nhà sản xuất |
| requiresPrescription | bool | Yes | Cần kê đơn không |

**Response** (201 Created): `MedicationDto`.

---

#### PUT /api/v1/medications/{id}

Cập nhật thông tin thuốc.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.update`

**Request Body**:

```json
{
  "name": "Lisinopril 20mg",
  "genericName": "Lisinopril",
  "brandName": "Zestril",
  "dosageForm": "Viên nén",
  "strength": "20mg",
  "route": "Uống",
  "category": "Thuốc tim mạch",
  "manufacturer": "AstraZeneca",
  "requiresPrescription": true
}
```

**Response** (200 OK): `MedicationDto` đã cập nhật.

---

#### PUT /api/v1/medications/{id}/deactivate

Vô hiệu hóa thuốc (không cho kê đơn mới nhưng giữ lịch sử).

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.update`

**Response** (204 No Content).

---

### Prescription Endpoints

#### GET /api/v1/prescriptions

Lấy danh sách đơn thuốc với filter.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| page | int | 1 | Số trang |
| pageSize | int | 20 | Số lượng mỗi trang |
| patientId | GUID | null | Lọc theo bệnh nhân |
| status | string | null | Lọc theo trạng thái |

**Response** (200 OK):

```json
{
  "items": [
    {
      "id": "5550e840-e29b-41d4-a716-446655440015",
      "patientId": "550e8400-e29b-41d4-a716-446655440000",
      "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "medicationId": "3330e840-e29b-41d4-a716-446655440013",
      "medicationName": "Amlodipine 5mg",
      "strength": "5mg",
      "dosageForm": "Viên nén",
      "dosageInstructions": "Uống 1 viên mỗi sáng sau ăn",
      "route": "Uống",
      "quantity": 30,
      "refills": 2,
      "statusCode": "ACTIVE",
      "statusName": "Đang hiệu lực",
      "prescribedAt": "2026-07-16T10:15:00Z",
      "filledAt": null,
      "createdAt": "2026-07-16T10:15:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

---

#### GET /api/v1/prescriptions/{id}

Lấy chi tiết đơn thuốc.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.view`

**Response** (200 OK): `PrescriptionDto` (xem schema ở trên).

---

#### GET /api/v1/prescriptions/patient/{patientId}

Lấy danh sách đơn thuốc của một bệnh nhân.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.view`

**Response** (200 OK): Collection của `PrescriptionDto`.

---

#### POST /api/v1/prescriptions

Kê đơn thuốc mới. Yêu cầu ít nhất một medication.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.create`

**Request Body**:

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "medications": [
    {
      "medicationId": "3330e840-e29b-41d4-a716-446655440013",
      "medicationName": "Amlodipine 5mg",
      "strength": "5mg",
      "dosageForm": "Viên nén",
      "dosageInstructions": "Uống 1 viên mỗi sáng sau ăn",
      "route": "Uống",
      "quantity": 30,
      "refills": 2
    }
  ],
  "notes": "Kiểm tra HA sau 2 tuần, nếu chưa ổn định tăng lên 10mg"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| patientId | GUID | Yes | ID bệnh nhân |
| providerId | GUID | Yes | ID bác sĩ kê đơn |
| medications | array | Yes | Danh sách thuốc (ít nhất 1) |
| medications[].medicationId | GUID | No | ID thuốc trong danh mục |
| medications[].medicationName | string | Yes | Tên thuốc |
| medications[].strength | string | Yes | Hàm lượng |
| medications[].dosageForm | string | Yes | Dạng bào chế |
| medications[].dosageInstructions | string | Yes | Hướng dẫn sử dụng |
| medications[].route | string | No | Đường dùng |
| medications[].quantity | int | Yes | Số lượng |
| medications[].refills | int | Yes | Số lần tái cấp |
| notes | string | No | Ghi chú của bác sĩ |

**Response** (201 Created): `PrescriptionDto`.

**Status Codes**:
| Code | Description |
|---|---|
| 201 | Tạo thành công |
| 400 | Không có thuốc trong đơn (ít nhất 1 medication required) |

---

#### PUT /api/v1/prescriptions/{id}/fill

Xuất thuốc (fill prescription) - cập nhật số lần tái cấp và đánh dấu ngày xuất.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.dispense`

**Response** (204 No Content).

---

#### PUT /api/v1/prescriptions/{id}/cancel

Hủy đơn thuốc.

- **Auth**: Bearer JWT
- **Permission**: `pharmacy.cancel`

**Request Body**:

```json
{
  "reason": "Bệnh nhân dị ứng với thành phần thuốc - đổi sang thuốc khác"
}
```

**Response** (204 No Content).

---

## Health Check

Tất cả services đều có health check endpoint:

### GET /health

- **Auth**: Không yêu cầu (AllowAnonymous)

**Response** (200 OK):

```json
{
  "status": "Healthy",
  "duration": 45.2,
  "checks": [
    {
      "name": "patient-db",
      "status": "Healthy",
      "description": null,
      "tags": ["database"],
      "error": null,
      "duration": 3.1
    },
    {
      "name": "rabbitmq",
      "status": "Healthy",
      "description": null,
      "tags": [],
      "error": null,
      "duration": 15.8
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": null,
      "tags": [],
      "error": null,
      "duration": 2.4
    }
  ]
}
```

---

## Common Patterns

### Authentication Flow

1. Client gửi `POST /api/v1/auth/login` với username/password
2. Nhận `accessToken` (JWT, hết hạn ngắn) và `refreshToken` (opaque, lưu trong Redis)
3. Gửi `Authorization: Bearer <accessToken>` cho tất cả request tiếp theo
4. Khi accessToken hết hạn, gọi `POST /api/v1/auth/refresh` với refreshToken
5. Đăng xuất: gọi `POST /api/v1/auth/logout` để hủy refreshToken

### Caching

Hệ thống sử dụng Redis caching:
- **Patient chi tiết**: TTL 5 phút, key `patient:{id}`
- **Patient search**: TTL 2 phút, key `patients:search:{q}:{page}:{pageSize}`
- **Appointment**: TTL 5 phút, invalidate khi có thay đổi
- **Invoice**: TTL 5 phút, invalidate khi có thay đổi
- **Dashboard stats**: TTL 2 phút

Cache được tự động invalidate khi thực hiện các thao tác CUD (Create/Update/Delete).

### gRPC Inter-Service Communication

Các service giao tiếp với nhau qua gRPC:
- AppointmentService gọi PatientService để verify patient tồn tại
- Tất cả gRPC server có interceptor authentication (JWT validation)

### Event-Driven Integration

Khi một service thực hiện thay đổi quan trọng, nó publish integration event lên RabbitMQ:
- `POST /api/v1/patients` -> `PatientRegisteredIntegrationEvent`
- `PUT /api/v1/patients/{id}` -> `PatientUpdatedIntegrationEvent`
- `POST /api/v1/appointments` -> `AppointmentScheduledIntegrationEvent`
- `POST /api/v1/encounters` -> `EncounterStartedIntegrationEvent`
- `POST /api/v1/lab-orders` -> `LabOrderCreatedIntegrationEvent`
- `PUT /api/v1/lab-orders/{id}/submit` -> `LabOrderSubmittedIntegrationEvent`
- `POST /api/v1/invoices` -> `InvoiceCreatedIntegrationEvent`
- `POST /api/v1/invoices/{id}/payments` -> `InvoicePaidIntegrationEvent`
- `POST /api/v1/prescriptions` -> `PrescriptionCreatedIntegrationEvent`

### Security Headers

Tất cả response đều có security headers (qua `UseSecurityHeaders()`):
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: strict-origin-when-cross-origin`

### Rate Limiting

Tất cả services có rate limiting (qua `UseRateLimiting()`) để chống brute-force và DoS.

### PHI Audit

Tất cả request đến các endpoint y tế đều được audit (qua `UsePhiAudit()` middleware) để tuân thủ HIPAA 164.312(b).

### Content Types

- Request: `application/json`
- Response: `application/json`
- Health check: `application/json`
