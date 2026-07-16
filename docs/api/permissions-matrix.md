# His.Hope Permission Matrix

## Role Definitions

| Role | Mô tả | Quyền hạn chính |
|---|---|---|
| **Admin** | Quản trị viên hệ thống | Toàn quyền: users, roles, permissions, settings, audit logs, và tất cả nghiệp vụ |
| **Provider** | Bác sĩ điều trị | Khám lâm sàng, kê đơn, chỉ định xét nghiệm, xem bệnh nhân và lịch hẹn |
| **Nurse** | Điều dưỡng | Ghi nhận sinh hiệu, check-in, xem bệnh nhân và lịch hẹn |
| **Receptionist** | Lễ tân / Tiếp đón | Đăng ký bệnh nhân, đặt lịch hẹn, check-in, xem danh sách |
| **LabTech** | Kỹ thuật viên xét nghiệm | Nhận mẫu, chạy xét nghiệm, nhập kết quả, xem bệnh nhân |
| **Pharmacist** | Dược sĩ | Quản lý danh mục thuốc, xuất thuốc theo đơn, xem đơn thuốc và bệnh nhân |
| **BillingClerk** | Nhân viên thu ngân / Kế toán | Tạo hóa đơn, ghi nhận thanh toán, void hóa đơn, xem hóa đơn và bệnh nhân |

## Legend

- ✅ = Có quyền
- -- = Không có quyền
- \* = Quyền chỉ có ở role có phạm vi hạn chế (role-specific)

---

## IdentityService

### Authentication Endpoints

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| POST /api/v1/auth/login | *Không* | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| POST /api/v1/auth/register | *Không* | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| POST /api/v1/auth/refresh | *Không* | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| POST /api/v1/auth/logout | *Authenticated* | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| POST /api/v1/auth/revoke | admin.users.write | ✅ | — | — | — | — | — | — |
| GET /api/v1/auth/me | *Authenticated* | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### User Management

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/auth/users | admin.users.read | ✅ | — | — | — | — | — | — |
| GET /api/v1/auth/users/{id} | admin.users.read | ✅ | — | — | — | — | — | — |
| POST /api/v1/auth/users | admin.users.write | ✅ | — | — | — | — | — | — |
| PUT /api/v1/auth/users/{id} | admin.users.write | ✅ | — | — | — | — | — | — |
| PUT /api/v1/auth/users/{id}/deactivate | admin.users.write | ✅ | — | — | — | — | — | — |
| PUT /api/v1/auth/users/{id}/activate | admin.users.write | ✅ | — | — | — | — | — | — |
| PUT /api/v1/auth/users/{id}/roles | admin.roles.write | ✅ | — | — | — | — | — | — |

### Role & Permission Management

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/auth/roles | admin.roles.read | ✅ | — | — | — | — | — | — |
| GET /api/v1/auth/roles/{id} | admin.roles.read | ✅ | — | — | — | — | — | — |
| POST /api/v1/auth/roles | admin.roles.write | ✅ | — | — | — | — | — | — |
| PUT /api/v1/auth/roles/{id} | admin.roles.write | ✅ | — | — | — | — | — | — |
| DELETE /api/v1/auth/roles/{id} | admin.roles.write | ✅ | — | — | — | — | — | — |
| GET /api/v1/auth/permissions | admin.permissions.read | ✅ | — | — | — | — | — | — |

### Settings & Audit

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/settings | admin.settings.read | ✅ | — | — | — | — | — | — |
| GET /api/v1/settings/{key} | admin.settings.read | ✅ | — | — | — | — | — | — |
| PUT /api/v1/settings/{key} | admin.settings.write | ✅ | — | — | — | — | — | — |
| PUT /api/v1/settings | admin.settings.write | ✅ | — | — | — | — | — | — |
| GET /api/v1/audit-logs | admin.audit.read | ✅ | — | — | — | — | — | — |
| GET /api/v1/audit-logs/{id} | admin.audit.read | ✅ | — | — | — | — | — | — |

---

## PatientService

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/patients/search | patients.view | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| GET /api/v1/patients/{id} | patients.view | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| POST /api/v1/patients | patients.create | ✅ | ✅ | — | ✅ | — | — | — |
| PUT /api/v1/patients/{id} | patients.update | ✅ | ✅ | ✅ | — | — | — | — |
| PATCH /api/v1/patients/{id}/deactivate | patients.delete | ✅ | — | — | — | — | — | — |
| PATCH /api/v1/patients/{id}/reactivate | patients.update | ✅ | ✅ | — | — | — | — | — |

---

## AppointmentService

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/appointments | appointments.view | ✅ | ✅ | ✅ | ✅ | — | — | — |
| GET /api/v1/appointments/search | appointments.view | ✅ | ✅ | ✅ | ✅ | — | — | — |
| GET /api/v1/appointments/{id} | appointments.view | ✅ | ✅ | ✅ | ✅ | — | — | — |
| GET /api/v1/appointments/patient/{patientId} | appointments.view | ✅ | ✅ | ✅ | ✅ | — | — | — |
| POST /api/v1/appointments | appointments.create | ✅ | ✅ | — | ✅ | — | — | — |
| PUT /api/v1/appointments/{id}/cancel | appointments.cancel | ✅ | ✅ | — | ✅ | — | — | — |
| PUT /api/v1/appointments/{id}/checkin | appointments.check-in | ✅ | ✅ | ✅ | ✅ | — | — | — |
| PUT /api/v1/appointments/{id}/checkout | appointments.update | ✅ | ✅ | — | ✅ | — | — | — |

---

## ClinicalService

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/encounters | clinical.view | ✅ | ✅ | ✅ | — | — | — | — |
| GET /api/v1/encounters/search | clinical.view | ✅ | ✅ | ✅ | — | — | — | — |
| GET /api/v1/encounters/{id} | clinical.view | ✅ | ✅ | ✅ | — | — | — | — |
| GET /api/v1/encounters/patient/{patientId} | clinical.view | ✅ | ✅ | ✅ | — | — | — | — |
| POST /api/v1/encounters | clinical.create | ✅ | ✅ | — | — | — | — | — |
| POST /api/v1/encounters/{id}/vitals | clinical.update | ✅ | ✅ | ✅ | — | — | — | — |
| POST /api/v1/encounters/{id}/diagnosis | clinical.update | ✅ | ✅ | — | — | — | — | — |
| PUT /api/v1/encounters/{id}/complete | clinical.update | ✅ | ✅ | — | — | — | — | — |
| GET /api/v1/dashboard/stats | reports.view | ✅ | — | — | — | — | — | — |

---

## LabService

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/lab-orders | lab.view | ✅ | ✅ | ✅ | — | ✅ | — | — |
| GET /api/v1/lab-orders/{id} | lab.view | ✅ | ✅ | ✅ | — | ✅ | — | — |
| GET /api/v1/lab-orders/patient/{patientId} | lab.view | ✅ | ✅ | ✅ | — | ✅ | — | — |
| POST /api/v1/lab-orders | lab.create | ✅ | ✅ | — | — | — | — | — |
| PUT /api/v1/lab-orders/{id}/submit | lab.update | ✅ | — | — | — | ✅ | — | — |
| PUT /api/v1/lab-orders/{id}/collect | lab.update | ✅ | — | — | — | ✅ | — | — |
| PUT /api/v1/lab-orders/{id}/result | lab.result | ✅ | — | — | — | ✅ | — | — |
| PUT /api/v1/lab-orders/{id}/cancel | lab.cancel | ✅ | ✅ | — | — | ✅ | — | — |

---

## BillingService

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/invoices | billing.view | ✅ | — | — | — | — | — | ✅ |
| GET /api/v1/invoices/{id} | billing.view | ✅ | — | — | — | — | — | ✅ |
| GET /api/v1/invoices/number/{invoiceNumber} | billing.view | ✅ | — | — | — | — | — | ✅ |
| GET /api/v1/invoices/patient/{patientId} | billing.view | ✅ | — | — | — | — | — | ✅ |
| POST /api/v1/invoices | billing.create | ✅ | — | — | — | — | — | ✅ |
| POST /api/v1/invoices/{id}/payments | billing.pay | ✅ | — | — | — | — | — | ✅ |
| PUT /api/v1/invoices/{id}/void | billing.void | ✅ | — | — | — | — | — | ✅ |

---

## PharmacyService

### Medication Management

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/medications | pharmacy.view | ✅ | ✅ | ✅ | — | — | ✅ | — |
| GET /api/v1/medications/{id} | pharmacy.view | ✅ | ✅ | ✅ | — | — | ✅ | — |
| POST /api/v1/medications | pharmacy.create | ✅ | — | — | — | — | ✅ | — |
| PUT /api/v1/medications/{id} | pharmacy.update | ✅ | — | — | — | — | ✅ | — |
| PUT /api/v1/medications/{id}/deactivate | pharmacy.update | ✅ | — | — | — | — | ✅ | — |

### Prescription Management

| Endpoint | Permission | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|---|
| GET /api/v1/prescriptions | pharmacy.view | ✅ | ✅ | — | — | — | ✅ | — |
| GET /api/v1/prescriptions/{id} | pharmacy.view | ✅ | ✅ | — | — | — | ✅ | — |
| GET /api/v1/prescriptions/patient/{patientId} | pharmacy.view | ✅ | ✅ | — | — | — | ✅ | — |
| POST /api/v1/prescriptions | pharmacy.create | ✅ | ✅ | — | — | — | — | — |
| PUT /api/v1/prescriptions/{id}/fill | pharmacy.dispense | ✅ | — | — | — | — | ✅ | — |
| PUT /api/v1/prescriptions/{id}/cancel | pharmacy.cancel | ✅ | ✅ | — | — | — | ✅ | — |

---

## Permission → Role Mapping Summary

| Permission Code | Admin | Provider | Nurse | Receptionist | LabTech | Pharmacist | BillingClerk |
|---|---|---|---|---|---|---|---|
| **patients.view** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **patients.create** | ✅ | ✅ | — | ✅ | — | — | — |
| **patients.update** | ✅ | ✅ | ✅ | — | — | — | — |
| **patients.delete** | ✅ | — | — | — | — | — | — |
| **appointments.view** | ✅ | ✅ | ✅ | ✅ | — | — | — |
| **appointments.create** | ✅ | ✅ | — | ✅ | — | — | — |
| **appointments.cancel** | ✅ | ✅ | — | ✅ | — | — | — |
| **appointments.check-in** | ✅ | ✅ | ✅ | ✅ | — | — | — |
| **appointments.update** | ✅ | ✅ | — | ✅ | — | — | — |
| **clinical.view** | ✅ | ✅ | ✅ | — | — | — | — |
| **clinical.create** | ✅ | ✅ | — | — | — | — | — |
| **clinical.update** | ✅ | ✅ | ✅* | — | — | — | — |
| **clinical.sign** | ✅ | ✅ | — | — | — | — | — |
| **lab.view** | ✅ | ✅ | ✅ | — | ✅ | — | — |
| **lab.create** | ✅ | ✅ | — | — | — | — | — |
| **lab.update** | ✅ | — | — | — | ✅ | — | — |
| **lab.result** | ✅ | — | — | — | ✅ | — | — |
| **lab.cancel** | ✅ | ✅ | — | — | ✅ | — | — |
| **billing.view** | ✅ | — | — | — | — | — | ✅ |
| **billing.create** | ✅ | — | — | — | — | — | ✅ |
| **billing.pay** | ✅ | — | — | — | — | — | ✅ |
| **billing.void** | ✅ | — | — | — | — | — | ✅ |
| **pharmacy.view** | ✅ | ✅ | ✅ | — | — | ✅ | — |
| **pharmacy.create** | ✅ | ✅ | — | — | — | ✅ | — |
| **pharmacy.update** | ✅ | — | — | — | — | ✅ | — |
| **pharmacy.dispense** | ✅ | — | — | — | — | ✅ | — |
| **pharmacy.cancel** | ✅ | ✅ | — | — | — | ✅ | — |
| **reports.view** | ✅ | — | — | — | — | — | — |
| **admin.users.read** | ✅ | — | — | — | — | — | — |
| **admin.users.write** | ✅ | — | — | — | — | — | — |
| **admin.roles.read** | ✅ | — | — | — | — | — | — |
| **admin.roles.write** | ✅ | — | — | — | — | — | — |
| **admin.permissions.read** | ✅ | — | — | — | — | — | — |
| **admin.settings.read** | ✅ | — | — | — | — | — | — |
| **admin.settings.write** | ✅ | — | — | — | — | — | — |
| **admin.audit.read** | ✅ | — | — | — | — | — | — |

> \* Nurse có `clinical.update` nhưng trong thực tế chỉ để ghi nhận sinh hiệu (vitals), không có quyền thêm chẩn đoán theo workflow lâm sàng thông thường.

---

## Ghi chú về Authorization Implementation

### Cơ chế Permission-Based Authorization

His.Hope sử dụng policy-based authorization của ASP.NET Core. Mỗi endpoint được bảo vệ bởi một policy tương ứng với permission:

```csharp
// Trong Program.cs
patients.MapGet("/{id:guid}", ...).RequireAuthorization("Permission:patients.view");
```

Các policy này được định nghĩa trong `AddHisHopeAuthorization()` extension method, tự động map permission codes thành ASP.NET Core authorization policies dựa trên claims trong JWT token.

### JWT Token Claims

JWT access token chứa các claims:
- `sub` / `nameidentifier`: User ID
- `permissions`: Mảng các permission codes

```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "username": "bs.nguyen",
  "permissions": [
    "patients.view",
    "patients.create",
    "patients.update",
    "clinical.view",
    "clinical.create",
    "clinical.update",
    "clinical.sign",
    "appointments.view",
    "appointments.create",
    "appointments.cancel",
    "lab.view",
    "lab.create",
    "pharmacy.view",
    "pharmacy.create"
  ]
}
```

### Role → Permission Mapping (Mặc định của hệ thống)

```
Admin       → Tất cả 36 permissions
Provider    → patients.{view,create,update} + appointments.{view,create,cancel} + clinical.{view,create,update,sign} + lab.{view,create,cancel} + pharmacy.{view,create,cancel}
Nurse       → patients.{view,update} + appointments.{view,check-in} + clinical.{view,update} + lab.view + pharmacy.view
Receptionist → patients.{view,create} + appointments.{view,create,cancel,check-in,update}
LabTech     → patients.view + lab.{view,update,result,cancel}
Pharmacist  → patients.view + pharmacy.{view,create,update,dispense,cancel}
BillingClerk → patients.view + billing.{view,create,pay,void}
```
