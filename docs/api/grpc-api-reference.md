# His.Hope gRPC API Reference

## Tổng quan

His.Hope sử dụng gRPC cho internal inter-service communication ngoài REST API. gRPC cung cấp hiệu năng cao hơn với Protocol Buffers serialization và HTTP/2 multiplexing.

### Authentication

Tất cả gRPC calls yêu cầu JWT Bearer token trong gRPC metadata:

```
Metadata: authorization = Bearer <access_token>
```

Mỗi gRPC service đều có `GrpcServerInterceptor` để validate JWT token trước khi xử lý request.

### Service Ports

| Service | gRPC HTTPS Port | gRPC HTTP Port | Package |
|---|---|---|---|
| PatientService | 5006 | 5013 | `his.hope.patient` |
| IdentityService | 5007 | — | (nội bộ) |
| AppointmentService | 5007 | 5014 | `his.hope.appointment` |
| ClinicalService | 5015 | 5016 | `his.hope.clinical` |
| LabService | 5020 | 5019 | `his.hope.lab` |
| BillingService | 5025 | 5026 | `his.hope.billing` |
| PharmacyService | 5015 | 5016 | `his.hope.pharmacy` |

> **Note**: Một số service chia sẻ port trên các Kestrel endpoint khác nhau. REST port dùng `Http1AndHttp2` protocol, gRPC port dùng `Http2` protocol riêng.

---

## PatientGrpcService

**Package**: `his.hope.patient`
**C# Namespace**: `His.Hope.PatientGrpc`
**Proto**: `src/Shared/Protos/patient.proto`

### RPCs

#### GetPatient

Lấy thông tin một bệnh nhân theo ID.

| Field | Value |
|---|---|
| Request | `PatientRequest` |
| Response | `PatientResponse` |

**Request Schema**:

```protobuf
message PatientRequest {
  string id = 1; // GUID của bệnh nhân
}
```

**Response Schema**:

```protobuf
message PatientResponse {
  string id = 1;
  string full_name = 2;
  string first_name = 3;
  string last_name = 4;
  string middle_name = 5;
  google.protobuf.Timestamp date_of_birth = 6;
  string gender_code = 7;
  string gender_name = 8;
  string phone = 9;
  string email = 10;
  bool is_active = 11;
  google.protobuf.Timestamp created_at = 12;
  google.protobuf.Timestamp updated_at = 13;
}
```

**Example JSON Response**:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "fullName": "Nguyễn Thị Hương",
  "firstName": "Thị",
  "lastName": "Nguyễn",
  "middleName": "Hương",
  "dateOfBirth": "1985-03-15T00:00:00Z",
  "genderCode": "F",
  "genderName": "Nữ",
  "phone": "0987654321",
  "email": "huong.nguyen@gmail.com",
  "isActive": true,
  "createdAt": "2025-06-10T08:00:00Z",
  "updatedAt": "2026-03-20T14:30:00Z"
}
```

---

#### SearchPatients

Tìm kiếm bệnh nhân với phân trang.

| Field | Value |
|---|---|
| Request | `PatientSearchRequest` |
| Response | `PatientListResponse` |

**Request Schema**:

```protobuf
message PatientSearchRequest {
  string search_term = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

**Response Schema**:

```protobuf
message PatientListResponse {
  repeated PatientResponse patients = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}
```

**Example JSON Response**:

```json
{
  "patients": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "fullName": "Nguyễn Thị Hương",
      "firstName": "Thị",
      "lastName": "Nguyễn",
      "middleName": "Hương",
      "dateOfBirth": "1985-03-15T00:00:00Z",
      "genderCode": "F",
      "genderName": "Nữ",
      "phone": "0987654321",
      "email": "huong.nguyen@gmail.com",
      "isActive": true,
      "createdAt": "2025-06-10T08:00:00Z",
      "updatedAt": "2026-03-20T14:30:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

---

#### CheckPatientExists

Kiểm tra bệnh nhân có tồn tại không (dùng cho validation từ service khác).

| Field | Value |
|---|---|
| Request | `PatientExistsRequest` |
| Response | `PatientExistsResponse` |

**Request Schema**:

```protobuf
message PatientExistsRequest {
  string id = 1;
}
```

**Response Schema**:

```protobuf
message PatientExistsResponse {
  bool exists = 1;
}
```

**Usage**: AppointmentService gọi RPC này để xác nhận patient tồn tại trước khi tạo lịch hẹn.

```csharp
var existsResponse = await patientClient.CheckPatientExistsAsync(
    new PatientExistsRequest { Id = request.PatientId.ToString() });
if (!existsResponse.Exists) return Results.NotFound();
```

---

## AppointmentGrpcService

**Package**: `his.hope.appointment`
**C# Namespace**: `His.Hope.AppointmentGrpc`
**Proto**: `src/Shared/Protos/appointment.proto`

### RPCs

#### GetAppointment

Lấy thông tin một lịch hẹn.

| Field | Value |
|---|---|
| Request | `AppointmentRequest` |
| Response | `AppointmentResponse` |

**Request Schema**:

```protobuf
message AppointmentRequest {
  string id = 1;
}
```

**Response Schema**:

```protobuf
message AppointmentResponse {
  string id = 1;
  string patient_id = 2;
  string provider_id = 3;
  google.protobuf.Timestamp scheduled_date = 4;
  google.protobuf.Timestamp start_time = 5;
  google.protobuf.Timestamp end_time = 6;
  string status_code = 7;
  string status_name = 8;
  string type_code = 9;
  google.protobuf.Timestamp created_at = 10;
  google.protobuf.Timestamp updated_at = 11;
}
```

**Example JSON Response**:

```json
{
  "id": "880e8400-e29b-41d4-a716-446655440003",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "scheduledDate": "2026-07-18T00:00:00Z",
  "startTime": "1970-01-01T14:00:00Z",
  "endTime": "1970-01-01T14:30:00Z",
  "statusCode": "SCHEDULED",
  "statusName": "Đã lên lịch",
  "typeCode": "TAI_KHAM",
  "createdAt": "2026-07-16T09:15:00Z",
  "updatedAt": "2026-07-16T09:15:00Z"
}
```

---

#### GetPatientAppointments

Lấy danh sách lịch hẹn của một bệnh nhân.

| Field | Value |
|---|---|
| Request | `PatientAppointmentsRequest` |
| Response | `AppointmentListResponse` |

**Request Schema**:

```protobuf
message PatientAppointmentsRequest {
  string patient_id = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

**Response Schema**:

```protobuf
message AppointmentListResponse {
  repeated AppointmentResponse appointments = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}
```

---

#### CheckAppointmentExists

Kiểm tra lịch hẹn có tồn tại không.

| Field | Value |
|---|---|
| Request | `AppointmentExistsRequest` |
| Response | `AppointmentExistsResponse` |

```protobuf
message AppointmentExistsRequest {
  string id = 1;
}

message AppointmentExistsResponse {
  bool exists = 1;
}
```

---

## ClinicalGrpcService

**Package**: `his.hope.clinical`
**C# Namespace**: `His.Hope.ClinicalGrpc`
**Proto**: `src/Shared/Protos/clinical.proto`

### RPCs

#### GetEncounter

Lấy thông tin một lượt khám.

| Field | Value |
|---|---|
| Request | `EncounterRequest` |
| Response | `EncounterResponse` |

**Response Schema**:

```protobuf
message EncounterResponse {
  string id = 1;
  string patient_id = 2;
  string provider_id = 3;
  string appointment_id = 4;
  google.protobuf.Timestamp encounter_date = 5;
  string encounter_type_code = 6;
  string encounter_type_name = 7;
  string status_code = 8;
  string status_name = 9;
  string chief_complaint = 10;
  bool has_vitals = 11;
  int32 diagnosis_count = 12;
  google.protobuf.Timestamp created_at = 13;
  google.protobuf.Timestamp updated_at = 14;
}
```

**Example JSON Response**:

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
  "chiefComplaint": "Đau đầu, chóng mặt 3 ngày nay",
  "hasVitals": true,
  "diagnosisCount": 2,
  "createdAt": "2026-07-16T09:30:00Z",
  "updatedAt": "2026-07-16T09:40:00Z"
}
```

---

#### GetPatientEncounters

Lấy lịch sử khám của một bệnh nhân.

| Field | Value |
|---|---|
| Request | `PatientEncountersRequest` |
| Response | `EncounterListResponse` |

**Request Schema**:

```protobuf
message PatientEncountersRequest {
  string patient_id = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

---

#### SearchEncounters

Tìm kiếm lượt khám.

| Field | Value |
|---|---|
| Request | `EncounterSearchRequest` |
| Response | `EncounterListResponse` |

**Request Schema**:

```protobuf
message EncounterSearchRequest {
  string search_term = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

---

#### CheckEncounterExists

Kiểm tra lượt khám có tồn tại không.

| Field | Value |
|---|---|
| Request | `EncounterExistsRequest` |
| Response | `EncounterExistsResponse` |

```protobuf
message EncounterExistsRequest {
  string id = 1;
}

message EncounterExistsResponse {
  bool exists = 1;
}
```

---

## LabGrpcService

**Package**: `his.hope.lab`
**C# Namespace**: `His.Hope.LabGrpc`
**Proto**: `src/Shared/Protos/lab.proto`

### RPCs

#### GetLabOrder

Lấy thông tin một lab order kèm danh sách tests và kết quả.

| Field | Value |
|---|---|
| Request | `LabOrderRequest` |
| Response | `LabOrderResponse` |

**Response Schema**:

```protobuf
message LabOrderResponse {
  string id = 1;
  string patient_id = 2;
  string provider_id = 3;
  string encounter_id = 4;
  google.protobuf.Timestamp order_date = 5;
  string status_code = 6;
  string status_name = 7;
  string priority_code = 8;
  string priority_name = 9;
  string notes = 10;
  repeated LabTestResponse tests = 11;
}

message LabTestResponse {
  string id = 1;
  string test_code = 2;
  string test_name = 3;
  string specimen_type = 4;
  string status_code = 5;
  string status_name = 6;
  google.protobuf.Timestamp ordered_at = 7;
  google.protobuf.Timestamp collected_at = 8;
  google.protobuf.Timestamp completed_at = 9;
  LabResultResponse result = 10;
}

message LabResultResponse {
  string lab_result_id = 1;
  string value = 2;
  string unit = 3;
  string reference_range = 4;
  string abnormal_flag_code = 5;
  string abnormal_flag_name = 6;
  string result_status_code = 7;
  string result_status_name = 8;
  google.protobuf.Timestamp resulted_at = 9;
  string performed_by = 10;
  string notes = 11;
}
```

**Example JSON Response với kết quả xét nghiệm**:

```json
{
  "id": "bb0e8400-e29b-41d4-a716-446655440006",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "providerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "orderDate": "2026-07-16T09:45:00Z",
  "statusCode": "PARTIAL_RESULT",
  "statusName": "Có kết quả một phần",
  "priorityCode": "ROUTINE",
  "priorityName": "Thường quy",
  "notes": "Xét nghiệm máu định kỳ",
  "tests": [
    {
      "id": "cc0e8400-e29b-41d4-a716-446655440007",
      "testCode": "CBC",
      "testName": "Tổng phân tích tế bào máu",
      "specimenType": "Máu toàn phần",
      "statusCode": "COMPLETED",
      "statusName": "Hoàn thành",
      "orderedAt": "2026-07-16T09:45:00Z",
      "collectedAt": "2026-07-16T10:00:00Z",
      "completedAt": "2026-07-16T11:30:00Z",
      "result": {
        "labResultId": "abc12345-e29b-41d4-a716-446655440000",
        "value": "5.2",
        "unit": "10^3/μL",
        "referenceRange": "4.0 - 10.0",
        "abnormalFlagCode": "N",
        "abnormalFlagName": "Bình thường",
        "resultStatusCode": "FINAL",
        "resultStatusName": "Kết quả cuối cùng",
        "resultedAt": "2026-07-16T11:30:00Z",
        "performedBy": "KTV. Nguyễn Văn Tâm",
        "notes": "Kết quả trong giới hạn bình thường"
      }
    }
  ]
}
```

---

#### GetPatientLabOrders

Lấy danh sách lab orders của một bệnh nhân.

| Field | Value |
|---|---|
| Request | `PatientLabOrdersRequest` |
| Response | `LabOrderListResponse` |

**Request Schema**:

```protobuf
message PatientLabOrdersRequest {
  string patient_id = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

---

#### CheckLabOrderExists

Kiểm tra lab order có tồn tại không.

```protobuf
message LabOrderExistsRequest { string id = 1; }
message LabOrderExistsResponse { bool exists = 1; }
```

---

#### SearchLabOrders

Tìm kiếm lab orders.

| Field | Value |
|---|---|
| Request | `LabOrderSearchRequest` |
| Response | `LabOrderListResponse` |

**Request Schema**:

```protobuf
message LabOrderSearchRequest {
  string search_term = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

---

## BillingGrpcService

**Package**: `his.hope.billing`
**C# Namespace**: `His.Hope.BillingGrpc`
**Proto**: `src/Shared/Protos/billing.proto`

### RPCs

#### GetInvoice

Lấy chi tiết hóa đơn kèm line items và payments.

| Field | Value |
|---|---|
| Request | `InvoiceRequest` |
| Response | `InvoiceResponse` |

**Response Schema**:

```protobuf
message InvoiceResponse {
  string id = 1;
  string patient_id = 2;
  string encounter_id = 3;
  string invoice_number = 4;
  google.protobuf.Timestamp invoice_date = 5;
  google.protobuf.Timestamp due_date = 6;
  string status_code = 7;
  string status_name = 8;
  string notes = 9;
  double sub_total = 10;
  double tax_amount = 11;
  double discount_amount = 12;
  double total_amount = 13;
  double paid_amount = 14;
  double balance_due = 15;
  google.protobuf.Timestamp created_at = 16;
  google.protobuf.Timestamp updated_at = 17;
  repeated InvoiceLineItemResponse line_items = 18;
  repeated PaymentResponse payments = 19;
}

message InvoiceLineItemResponse {
  string id = 1;
  string description = 2;
  int32 quantity = 3;
  double unit_price = 4;
  double amount = 5;
  string item_code = 6;
  string item_type_code = 7;
  string item_type_name = 8;
}

message PaymentResponse {
  string id = 1;
  double amount = 2;
  google.protobuf.Timestamp payment_date = 3;
  string method_code = 4;
  string method_name = 5;
  string reference_number = 6;
  string status_code = 7;
  string status_name = 8;
}
```

**Example JSON Response (đã thanh toán một phần)**:

```json
{
  "id": "ff0e8400-e29b-41d4-a716-446655440010",
  "patientId": "550e8400-e29b-41d4-a716-446655440000",
  "encounterId": "aa0e8400-e29b-41d4-a716-446655440005",
  "invoiceNumber": "INV-20260716-0001",
  "invoiceDate": "2026-07-16T10:00:00Z",
  "dueDate": "2026-07-23T00:00:00Z",
  "statusCode": "PARTIALLY_PAID",
  "statusName": "Thanh toán một phần",
  "notes": "Hóa đơn khám ngoại trú",
  "subTotal": 850000.0,
  "taxAmount": 68000.0,
  "discountAmount": 50000.0,
  "totalAmount": 868000.0,
  "paidAmount": 500000.0,
  "balanceDue": 368000.0,
  "createdAt": "2026-07-16T10:00:00Z",
  "updatedAt": "2026-07-16T11:30:00Z",
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
    }
  ],
  "payments": [
    {
      "id": "6660e840-e29b-41d4-a716-446655440016",
      "amount": 500000.0,
      "paymentDate": "2026-07-16T11:30:00Z",
      "methodCode": "CASH",
      "methodName": "Tiền mặt",
      "referenceNumber": "PT-20260716-0001",
      "statusCode": "COMPLETED",
      "statusName": "Hoàn thành"
    }
  ]
}
```

---

#### GetInvoiceByNumber

Lấy hóa đơn theo số hóa đơn.

| Field | Value |
|---|---|
| Request | `InvoiceByNumberRequest` |
| Response | `InvoiceResponse` |

```protobuf
message InvoiceByNumberRequest {
  string invoice_number = 1;
}
```

---

#### GetPatientInvoices

Lấy danh sách hóa đơn của một bệnh nhân.

| Field | Value |
|---|---|
| Request | `PatientInvoicesRequest` |
| Response | `InvoiceListResponse` |

```protobuf
message PatientInvoicesRequest {
  string patient_id = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

---

#### CheckInvoiceExists

Kiểm tra hóa đơn có tồn tại không.

```protobuf
message InvoiceExistsRequest { string id = 1; }
message InvoiceExistsResponse { bool exists = 1; }
```

---

#### SearchInvoices

Tìm kiếm hóa đơn.

| Field | Value |
|---|---|
| Request | `InvoiceSearchRequest` |
| Response | `InvoiceListResponse` |

```protobuf
message InvoiceSearchRequest {
  string search_term = 1;
  int32 page = 2;
  int32 page_size = 3;
}
```

---

## PharmacyGrpcService

**Package**: `his.hope.pharmacy`
**C# Namespace**: `His.Hope.PharmacyGrpc`
**Proto**: `src/Shared/Protos/pharmacy.proto`

### RPCs

#### GetMedication

Lấy thông tin một thuốc.

| Field | Value |
|---|---|
| Request | `MedicationRequest` |
| Response | `MedicationResponse` |

**Response Schema**:

```protobuf
message MedicationResponse {
  string id = 1;
  string name = 2;
  string generic_name = 3;
  string brand_name = 4;
  string dosage_form = 5;
  string strength = 6;
  string route = 7;
  bool requires_prescription = 8;
  bool is_active = 9;
  google.protobuf.Timestamp created_at = 10;
  google.protobuf.Timestamp updated_at = 11;
}
```

**Example JSON Response**:

```json
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
  "createdAt": "2025-01-10T00:00:00Z",
  "updatedAt": "2026-01-10T00:00:00Z"
}
```

---

#### SearchMedications

Tìm kiếm thuốc trong danh mục.

| Field | Value |
|---|---|
| Request | `MedicationSearchRequest` |
| Response | `MedicationListResponse` |

```protobuf
message MedicationSearchRequest {
  string search_term = 1;
  int32 page = 2;
  int32 page_size = 3;
}

message MedicationListResponse {
  repeated MedicationResponse medications = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}
```

---

#### CheckMedicationExists

Kiểm tra thuốc có tồn tại trong danh mục không.

```protobuf
message MedicationExistsRequest { string id = 1; }
message MedicationExistsResponse { bool exists = 1; }
```

---

#### GetPrescription

Lấy chi tiết một đơn thuốc.

| Field | Value |
|---|---|
| Request | `PrescriptionRequest` |
| Response | `PrescriptionResponse` |

**Response Schema**:

```protobuf
message PrescriptionResponse {
  string id = 1;
  string patient_id = 2;
  string provider_id = 3;
  string medication_id = 4;
  string medication_name = 5;
  string strength = 6;
  string dosage_form = 7;
  string dosage_instructions = 8;
  string route = 9;
  int32 quantity = 10;
  int32 refills = 11;
  string status_code = 12;
  string status_name = 13;
  google.protobuf.Timestamp prescribed_at = 14;
  google.protobuf.Timestamp filled_at = 15;
  google.protobuf.Timestamp created_at = 16;
  google.protobuf.Timestamp updated_at = 17;
}
```

**Example JSON Response**:

```json
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
  "createdAt": "2026-07-16T10:15:00Z",
  "updatedAt": "2026-07-16T10:15:00Z"
}
```

---

#### SearchPrescriptions

Tìm kiếm đơn thuốc.

| Field | Value |
|---|---|
| Request | `PrescriptionSearchRequest` |
| Response | `PrescriptionListResponse` |

```protobuf
message PrescriptionSearchRequest {
  string search_term = 1;
  int32 page = 2;
  int32 page_size = 3;
}

message PrescriptionListResponse {
  repeated PrescriptionResponse prescriptions = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}
```

---

## gRPC Client Usage Example

Dưới đây là cách AppointmentService tạo gRPC client để gọi PatientService:

```csharp
// Đăng ký client trong DI container (AppointmentService Program.cs)
builder.Services.AddSingleton(_ =>
{
    var handler = new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        UseProxy = false,
        AllowAutoRedirect = false,
    };
    var channel = GrpcChannel.ForAddress("http://localhost:5013", new GrpcChannelOptions
    {
        HttpHandler = handler,
        DisposeHttpClient = true,
        MaxRetryAttempts = 0,
    });
    return new PatientGrpcService.PatientGrpcServiceClient(channel);
});

// Sử dụng client trong endpoint handler
grp.MapPost("/", async (ScheduleAppointmentRequest request,
    PatientGrpcService.PatientGrpcServiceClient patientClient, ...) =>
{
    var existsResponse = await patientClient.CheckPatientExistsAsync(
        new PatientExistsRequest { Id = request.PatientId.ToString() });

    if (!existsResponse.Exists)
        return Results.Problem("Patient not found", statusCode: 404);
    // ...
});
```

### gRPC Health Checks

Tất cả services đều có gRPC health probe:

```csharp
app.MapGrpcHealthChecksService();
```

### gRPC Server Interceptor

Mỗi service có `GrpcServerInterceptor` để thực thi các cross-cutting concerns:
- JWT Authentication validation từ gRPC metadata header `authorization`
- Request logging
- Correlation ID propagation

```csharp
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerInterceptor>();
});
```
