# His.Hope Domain Events Reference

## Tổng quan

His.Hope sử dụng kiến trúc event-driven với RabbitMQ làm message broker. Các service publish **Integration Events** (external domain events) lên RabbitMQ khi có thay đổi quan trọng trong hệ thống, cho phép các service khác subscribe và phản ứng theo kiểu loose coupling.

Ngoài ra, mỗi service còn có **Domain Events** nội bộ (internal) được dispatch trong cùng process qua MediatR, sử dụng Outbox Pattern để đảm bảo transactional consistency giữa database write và message publish.

### Event Infrastructure

| Component | Technology |
|---|---|
| Message Broker | RabbitMQ |
| Event Bus Abstraction | `His.Hope.EventBus.Abstractions.IntegrationEvent` |
| Provider | `His.Hope.EventBusRabbitMQ` |
| Outbox Pattern | `His.Hope.Infrastructure.Outbox` |
| In-process Events | MediatR `INotification` + Domain Event Dispatcher |

### Base Event Schema

Tất cả Integration Event kế thừa từ `IntegrationEvent` base class:

```json
{
  "id": "guid",           // Unique event ID (auto-generated)
  "creationDate": "UTC"   // Event creation timestamp (auto-generated)
}
```

### Exchange & Routing Key Convention

| Event | Exchange | Routing Key |
|---|---|---|
| PatientRegistered | `his_hope_patient` | `Patient.PatientRegistered` |
| PatientUpdated | `his_hope_patient` | `Patient.PatientUpdated` |
| AppointmentScheduled | `his_hope_appointment` | `Appointment.AppointmentScheduled` |
| EncounterStarted | `his_hope_clinical` | `Clinical.EncounterStarted` |
| LabOrderCreated | `labdb` | `Lab.LabOrderCreated` |
| LabOrderSubmitted | `labdb` | `Lab.LabOrderSubmitted` |
| InvoiceCreated | `billingdb` | `Billing.InvoiceCreated` |
| InvoicePaid | `billingdb` | `Billing.InvoicePaid` |
| PrescriptionCreated | `pharmacydb` | `Pharmacy.PrescriptionCreated` |

> **Note**: Routing key format được suy diễn từ namespace + class name của Integration Event, theo convention của RabbitMQ Event Bus implementation.

---

## Domain Events (Internal - MediatR)

Các domain event nội bộ được dispatch trong cùng process, KHÔNG publish lên RabbitMQ. Chúng được xử lý bởi các handler trong cùng service.

### PatientService Domain Events

#### PatientRegisteredDomainEvent

**File**: `src/Services/PatientService/PatientService.Domain/Events/PatientRegisteredDomainEvent.cs`

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "fullName": "Nguyễn Thị Hương",
  "occurredOn": "2026-07-16T09:00:00Z"
}
```

**Handler**: PatientRegisteredIntegrationEvent được publish lên RabbitMQ sau khi domain event xử lý xong.

---

#### PatientUpdatedDomainEvent

**File**: `src/Services/PatientService/PatientService.Domain/Events/PatientUpdatedDomainEvent.cs`

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "fullName": "Nguyễn Thị Hương (đã cập nhật)",
  "occurredOn": "2026-07-16T09:30:00Z"
}
```

---

#### PatientDeactivatedDomainEvent

**File**: `src/Services/PatientService/PatientService.Domain/Events/PatientDeactivatedDomainEvent.cs`

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "reason": "Bệnh nhân yêu cầu xóa hồ sơ",
  "occurredOn": "2026-07-16T10:00:00Z"
}
```

---

#### PatientReactivatedDomainEvent

```json
{
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "occurredOn": "2026-07-16T10:30:00Z"
}
```

---

### ClinicalService Domain Events

#### EncounterStartedDomainEvent

```json
{
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "appointmentId": "880e8400-e29b-41d4-a716-446655440003",
  "encounterType": "KHAM_NGOAI_TRU",
  "occurredOn": "2026-07-16T09:30:00Z"
}
```

---

#### VitalsRecordedDomainEvent

```json
{
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "temperature": 37.5,
  "heartRate": 88,
  "systolicBP": 145,
  "diastolicBP": 92,
  "oxygenSaturation": 97.0,
  "occurredOn": "2026-07-16T09:35:00Z"
}
```

---

#### DiagnosisAddedDomainEvent

```json
{
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "conditionName": "Tăng huyết áp vô căn chưa kiểm soát",
  "icd10Code": "I10",
  "isPrimary": true,
  "occurredOn": "2026-07-16T09:40:00Z"
}
```

---

#### EncounterCompletedDomainEvent

```json
{
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "completedAt": "2026-07-16T09:50:00Z",
  "occurredOn": "2026-07-16T09:50:00Z"
}
```

---

### LabService Domain Events

#### LabOrderCreatedDomainEvent

```json
{
  "labOrderId": "bb0e8400-e29b-41d4-a716-446655440006",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "testCount": 3,
  "priorityCode": "ROUTINE",
  "occurredOn": "2026-07-16T09:45:00Z"
}
```

---

#### LabOrderSubmittedDomainEvent

```json
{
  "labOrderId": "bb0e8400-e29b-41d4-a716-446655440006",
  "occurredOn": "2026-07-16T09:46:00Z"
}
```

---

#### LabOrderCancelledDomainEvent

```json
{
  "labOrderId": "bb0e8400-e29b-41d4-a716-446655440006",
  "reason": "Bệnh nhân từ chối làm xét nghiệm",
  "occurredOn": "2026-07-16T09:47:00Z"
}
```

---

#### LabTestResultRecordedDomainEvent

```json
{
  "labOrderId": "bb0e8400-e29b-41d4-a716-446655440006",
  "testId": "cc0e8400-e29b-41d4-a716-446655440007",
  "testCode": "CBC",
  "testName": "Tổng phân tích tế bào máu",
  "value": "5.2",
  "unit": "10^3/μL",
  "abnormalFlagCode": "N",
  "abnormalFlagName": "Bình thường",
  "occurredOn": "2026-07-16T11:30:00Z"
}
```

---

### BillingService Domain Events

#### InvoiceCreatedDomainEvent

```json
{
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "invoiceNumber": "INV-20260716-0001",
  "totalAmount": 868000.0,
  "occurredOn": "2026-07-16T10:00:00Z"
}
```

---

#### InvoiceSubmittedDomainEvent

```json
{
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "occurredOn": "2026-07-16T10:01:00Z"
}
```

---

#### InvoicePaidDomainEvent

```json
{
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "amountPaid": 868000.0,
  "totalAmount": 868000.0,
  "isFullyPaid": true,
  "occurredOn": "2026-07-16T11:00:00Z"
}
```

---

#### InvoiceCancelledDomainEvent

```json
{
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "reason": "Sai thông tin bệnh nhân",
  "occurredOn": "2026-07-16T10:05:00Z"
}
```

---

#### InvoiceVoidedDomainEvent

```json
{
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "reason": "Sai thông tin bệnh nhân - tạo lại hóa đơn mới",
  "occurredOn": "2026-07-16T10:10:00Z"
}
```

---

#### PaymentRecordedDomainEvent

```json
{
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "paymentId": "6660e840-e29b-41d4-a716-446655440016",
  "amount": 868000.0,
  "methodCode": "CASH",
  "methodName": "Tiền mặt",
  "occurredOn": "2026-07-16T11:00:00Z"
}
```

---

### PharmacyService Domain Events

#### MedicationCreatedDomainEvent

```json
{
  "medicationId": "3330e840-e29b-41d4-a716-446655440013",
  "name": "Lisinopril 10mg",
  "genericName": "Lisinopril",
  "manufacturer": "AstraZeneca",
  "occurredOn": "2026-07-16T09:00:00Z"
}
```

---

#### MedicationUpdatedDomainEvent

```json
{
  "medicationId": "3330e840-e29b-41d4-a716-446655440013",
  "name": "Lisinopril 20mg",
  "changes": "Hàm lượng: 10mg → 20mg",
  "occurredOn": "2026-07-16T09:10:00Z"
}
```

---

#### MedicationDeactivatedDomainEvent

```json
{
  "medicationId": "3330e840-e29b-41d4-a716-446655440013",
  "name": "Lisinopril 20mg",
  "occurredOn": "2026-07-16T09:15:00Z"
}
```

---

#### MedicationReactivatedDomainEvent

```json
{
  "medicationId": "3330e840-e29b-41d4-a716-446655440013",
  "name": "Lisinopril 20mg",
  "occurredOn": "2026-07-16T09:20:00Z"
}
```

---

#### PrescriptionCreatedDomainEvent

```json
{
  "prescriptionId": "5550e840-e29b-41d4-a716-446655440015",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "medicationName": "Amlodipine 5mg",
  "strength": "5mg",
  "dosageForm": "Viên nén",
  "dosageInstructions": "Uống 1 viên mỗi sáng sau ăn",
  "quantity": 30,
  "refills": 2,
  "occurredOn": "2026-07-16T10:15:00Z"
}
```

---

#### PrescriptionFilledDomainEvent

```json
{
  "prescriptionId": "5550e840-e29b-41d4-a716-446655440015",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "medicationName": "Amlodipine 5mg",
  "quantity": 30,
  "remainingRefills": 1,
  "filledAt": "2026-07-17T08:00:00Z",
  "occurredOn": "2026-07-17T08:00:00Z"
}
```

---

#### PrescriptionCancelledDomainEvent

```json
{
  "prescriptionId": "5550e840-e29b-41d4-a716-446655440015",
  "reason": "Bệnh nhân dị ứng với thành phần thuốc",
  "occurredOn": "2026-07-16T10:20:00Z"
}
```

---

## Integration Events (RabbitMQ - Cross-Service)

Các Integration Event được publish lên RabbitMQ exchanges, cho phép các service khác subscribe.

### Patient Events

**Exchange**: `his_hope_patient`

#### PatientRegistered

**Type**: `His.Hope.IntegrationEvents.Patient.PatientRegisteredIntegrationEvent`
**Published by**: PatientService (khi POST /api/v1/patients)
**C# Constructor**:

```csharp
new PatientRegisteredIntegrationEvent(
    patientId: Guid, fullName: string, phone: string,
    genderCode: string, dateOfBirth: DateTime)
```

**JSON Schema**:

```json
{
  "id": "abc12345-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T09:00:00.0000000Z",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "fullName": "Nguyễn Thị Hương",
  "phone": "0987654321",
  "genderCode": "F",
  "dateOfBirth": "1985-03-15T00:00:00"
}
```

**Consumers**: NotificationService, ReportingService (nếu có)

---

#### PatientUpdated

**Type**: `His.Hope.IntegrationEvents.Patient.PatientUpdatedIntegrationEvent`
**Published by**: PatientService (khi PUT /api/v1/patients/{id})

```json
{
  "id": "def67890-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T09:30:00.0000000Z",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "fullName": "Nguyễn Thị Hương (đã cập nhật)",
  "phone": "0911223344"
}
```

---

### Appointment Events

**Exchange**: `his_hope_appointment`

#### AppointmentScheduled

**Type**: `His.Hope.IntegrationEvents.Appointment.AppointmentScheduledIntegrationEvent`
**Published by**: AppointmentService (khi POST /api/v1/appointments)

**C# Constructor**:

```csharp
new AppointmentScheduledIntegrationEvent(
    appointmentId: Guid, patientId: Guid, providerId: Guid,
    scheduledDate: DateTime, startTime: TimeSpan, endTime: TimeSpan)
```

```json
{
  "id": "ghi11223-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T09:15:00.0000000Z",
  "appointmentId": "880e8400-e29b-41d4-a716-446655440003",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "scheduledDate": "2026-07-18T00:00:00",
  "startTime": "14:00:00",
  "endTime": "14:30:00"
}
```

**Consumers**: NotificationService (gửi SMS/email nhắc lịch), ClinicalService (chuẩn bị hồ sơ)

---

### Clinical Events

**Exchange**: `his_hope_clinical`

#### EncounterStarted

**Type**: `His.Hope.IntegrationEvents.Clinical.EncounterStartedIntegrationEvent`
**Published by**: ClinicalService (khi POST /api/v1/encounters)

**C# Constructor**:

```csharp
new EncounterStartedIntegrationEvent(
    encounterId: Guid, patientId: Guid, providerId: Guid,
    appointmentId: Guid?, encounterTypeCode: string, encounterDate: DateTime)
```

```json
{
  "id": "jkl33445-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T09:30:00.0000000Z",
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "appointmentId": "880e8400-e29b-41d4-a716-446655440003",
  "encounterTypeCode": "KHAM_NGOAI_TRU",
  "encounterDate": "2026-07-16T09:30:00"
}
```

**Consumers**: BillingService (chuẩn bị hóa đơn), ReportingService

---

### Lab Events

**Exchange**: `labdb`

#### LabOrderCreated

**Type**: `His.Hope.IntegrationEvents.Lab.LabOrderCreatedIntegrationEvent`
**Published by**: LabService (khi POST /api/v1/lab-orders)

```json
{
  "id": "mno55667-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T09:45:00.0000000Z",
  "labOrderId": "bb0e8400-e29b-41d4-a716-446655440006",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Consumers**: BillingService (thêm lab tests vào hóa đơn nếu có)

---

#### LabOrderSubmitted

**Type**: `His.Hope.IntegrationEvents.Lab.LabOrderSubmittedIntegrationEvent`
**Published by**: LabService (khi PUT /api/v1/lab-orders/{id}/submit)

```json
{
  "id": "pqr77889-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T09:46:00.0000000Z",
  "labOrderId": "bb0e8400-e29b-41d4-a716-446655440006"
}
```

**Consumers**: NotificationService (thông báo cho LabTech), BillingService

---

### Billing Events

**Exchange**: `billingdb`

#### InvoiceCreated

**Type**: `His.Hope.IntegrationEvents.Billing.InvoiceCreatedIntegrationEvent`
**Published by**: BillingService (khi POST /api/v1/invoices)

```json
{
  "id": "stu99001-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T10:00:00.0000000Z",
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "invoiceNumber": "INV-20260716-0001",
  "totalAmount": 868000.0
}
```

**Consumers**: NotificationService (thông báo thanh toán), AccountingService

---

#### InvoicePaid

**Type**: `His.Hope.IntegrationEvents.Billing.InvoicePaidIntegrationEvent`
**Published by**: BillingService (khi payment làm invoice.statusCode thành PAID)

```json
{
  "id": "vwx11223-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T11:00:00.0000000Z",
  "invoiceId": "ff0e8400-e29b-41d4-a716-446655440010",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "amountPaid": 868000.0,
  "totalAmount": 868000.0
}
```

**Consumers**: NotificationService, AccountingService, ReportingService

> **Note**: Event này chỉ publish khi `invoice.StatusCode == "PAID"` (tức tổng payments >= totalAmount).

---

### Pharmacy Events

**Exchange**: `pharmacydb`

#### PrescriptionCreated

**Type**: `His.Hope.IntegrationEvents.Pharmacy.PrescriptionCreatedIntegrationEvent`
**Published by**: PharmacyService (khi POST /api/v1/prescriptions)

```json
{
  "id": "yza33445-e29b-41d4-a716-446655440000",
  "creationDate": "2026-07-16T10:15:00.0000000Z",
  "prescriptionId": "5550e840-e29b-41d4-a716-446655440015",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "medicationName": "Amlodipine 5mg",
  "strength": "5mg",
  "dosageForm": "Viên nén",
  "dosageInstructions": "Uống 1 viên mỗi sáng sau ăn",
  "quantity": 30,
  "refills": 2,
  "prescribedDate": "2026-07-16T10:15:00"
}
```

**Consumers**: BillingService (thêm thuốc vào hóa đơn), NotificationService, InventoryService

---

## Event Flow Diagrams

### Luồng khám bệnh điển hình (Outpatient Visit Flow)

```
1. POST /api/v1/patients           → PatientRegistered Event
2. POST /api/v1/appointments       → AppointmentScheduled Event
3. PUT  /api/v1/appointments/{id}/checkin
4. POST /api/v1/encounters         → EncounterStarted Event
        └── BillingService: chuẩn bị hóa đơn
5. POST /api/v1/encounters/{id}/vitals
6. POST /api/v1/encounters/{id}/diagnosis
7. POST /api/v1/lab-orders         → LabOrderCreated Event
        └── BillingService: thêm lab tests vào hóa đơn
8. PUT  /api/v1/lab-orders/{id}/submit → LabOrderSubmitted Event
9. PUT  /api/v1/lab-orders/{id}/result (có thể gọi nhiều lần)
10. POST /api/v1/prescriptions     → PrescriptionCreated Event
        └── BillingService: thêm thuốc vào hóa đơn
11. PUT  /api/v1/encounters/{id}/complete
12. POST /api/v1/invoices          → InvoiceCreated Event
13. POST /api/v1/invoices/{id}/payments → InvoicePaid Event (nếu đủ)
14. PUT  /api/v1/appointments/{id}/checkout
15. PUT  /api/v1/prescriptions/{id}/fill (khi bệnh nhân đến nhà thuốc)
```

### Luồng thanh toán (Billing Flow)

```
InvoiceCreated → [Invoice chờ thanh toán]
     │
     ├── RecordPayment (số tiền < totalAmount)
     │   └── Invoice status: PARTIALLY_PAID
     │
     └── RecordPayment (số tiền >= totalAmount)
         ├── Invoice status: PAID
         └── InvoicePaid Event → AccountingService, NotificationService
```

---

## Outbox Pattern Implementation

Tất cả Integration Event đều được publish qua Outbox Pattern để đảm bảo **transactional consistency**:

1. **Write Phase**: Khi xử lý command (VD: CreatePatient):
   - Domain event được dispatch (PatientRegisteredDomainEvent)
   - Domain event handler tạo Integration Event và lưu vào outbox table trong cùng transaction với database write

2. **Publish Phase**: Background service (Outbox Processor) quét outbox table:
   - Đọc các message chưa publish
   - Publish lên RabbitMQ
   - Đánh dấu đã publish

3. **Guarantees**:
   - At-least-once delivery
   - Idempotent consumers (consumer phải xử lý trùng lặp)
   - Không mất event nếu RabbitMQ tạm thời không khả dụng

```csharp
// Đăng ký Outbox trong mỗi service
builder.Services.AddOutbox<PatientDbContext>();
```

---

## Integration Event Class Hierarchy

```
His.Hope.EventBus.Abstractions.IntegrationEvent (base)
├── His.Hope.IntegrationEvents.Patient
│   ├── PatientRegisteredIntegrationEvent
│   └── PatientUpdatedIntegrationEvent
├── His.Hope.IntegrationEvents.Appointment
│   └── AppointmentScheduledIntegrationEvent
├── His.Hope.IntegrationEvents.Clinical
│   └── EncounterStartedIntegrationEvent
├── His.Hope.IntegrationEvents.Lab
│   ├── LabOrderCreatedIntegrationEvent
│   └── LabOrderSubmittedIntegrationEvent
├── His.Hope.IntegrationEvents.Billing
│   ├── InvoiceCreatedIntegrationEvent
│   └── InvoicePaidIntegrationEvent
└── His.Hope.IntegrationEvents.Pharmacy
    └── PrescriptionCreatedIntegrationEvent
```

