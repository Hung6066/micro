# His.Hope FHIR R4 API Reference

## Tổng quan

His.Hope FHIR Gateway cung cấp RESTful API tuân theo chuẩn HL7 FHIR R4 (v4.0.1) cho phép các hệ thống bên ngoài truy vấn dữ liệu bệnh nhân, lượt khám và các tài nguyên y tế khác thông qua giao diện chuẩn hóa.

### Base URL

| Environment | URL |
|---|---|
| Production | `https://fhir.his.hope.vn/fhir/r4` |
| Staging | `https://fhir-staging.his.hope.vn/fhir/r4` |
| Development | `https://localhost:5040/fhir/r4` |

### Content Type

Tất cả request và response sử dụng `application/fhir+json`:

- **Request Accept header**: `application/fhir+json` hoặc `application/json`
- **Response Content-Type**: `application/fhir+json; charset=utf-8`

### Authentication

Các endpoint FHIR (trừ `/metadata`) yêu cầu JWT Bearer token:

```http
Authorization: Bearer <access_token>
```

Token được cấp bởi His.Hope IdentityService qua endpoint `POST /api/v1/auth/login`.

### FHIR Version

- **FHIR Version**: 4.0.1 (R4)
- **Format hỗ trợ**: `application/fhir+json`, `application/json`

---

## CapabilityStatement

### GET /fhir/r4/metadata

Lấy CapabilityStatement mô tả các tính năng và tài nguyên FHIR được hỗ trợ bởi server.

- **Auth**: Không yêu cầu (AllowAnonymous)

**Response** (200 OK):

```json
{
  "resourceType": "CapabilityStatement",
  "status": "draft",
  "date": "2026-07-18T00:00:00+07:00",
  "publisher": "Bệnh viện Đa khoa X – His.Hope Platform",
  "kind": "instance",
  "software": {
    "name": "His.Hope FHIR Gateway",
    "version": "1.0.0"
  },
  "fhirVersion": "4.0.1",
  "format": ["application/fhir+json", "application/json"],
  "rest": [
    {
      "mode": "server",
      "security": {
        "cors": true,
        "description": "JWT Bearer token authentication via His.Hope IdentityService"
      },
      "resource": [
        {
          "type": "Patient",
          "profile": "http://hl7.org/fhir/StructureDefinition/Patient",
          "interaction": ["read", "search-type"],
          "searchParam": [
            {"name": "name", "type": "string", "documentation": "A patient name (partial match on any part of the name)"},
            {"name": "identifier", "type": "token", "documentation": "A patient identifier (His.Hope internal ID or external ID)"},
            {"name": "birthdate", "type": "date", "documentation": "The patient's date of birth (exact match: yyyy-MM-dd)"}
          ]
        },
        {
          "type": "Encounter",
          "profile": "http://hl7.org/fhir/StructureDefinition/Encounter",
          "interaction": ["read"]
        }
      ]
    }
  ]
}
```

---

## Patient Endpoints

### GET /fhir/r4/Patient/{id}

Truy xuất một Patient resource theo His.Hope internal ID.

- **Auth**: Bearer JWT
- **Permission**: `patients.view`

**Path Parameters**:

| Param | Type | Description |
|---|---|---|
| id | string | His.Hope patient UUID |

**Response** (200 OK):

```json
{
  "resourceType": "Patient",
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "identifier": [
    {
      "system": "https://his.hope.vn/patient-id",
      "value": "550e8400-e29b-41d4-a716-446655440000",
      "type": {
        "coding": [
          {
            "system": "http://terminology.hl7.org/CodeSystem/v2-0203",
            "code": "MR",
            "display": "Medical Record Number"
          }
        ]
      }
    }
  ],
  "name": [
    {
      "use": "official",
      "family": "Nguyễn",
      "given": ["Hương", "Thị"]
    }
  ],
  "gender": "female",
  "birthDate": "1985-03-15",
  "active": true,
  "telecom": [
    {"system": "phone", "value": "0987654321", "use": "mobile"},
    {"system": "email", "value": "huong.nguyen@example.com", "use": "home"}
  ]
}
```

**Status Codes**:

| Code | Description |
|---|---|
| 200 | Thành công |
| 401 | Token không hợp lệ hoặc hết hạn |
| 404 | Không tìm thấy Patient với ID đã cho |

**cURL Example**:

```bash
curl -k -X GET "https://fhir.his.hope.vn/fhir/r4/Patient/550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/fhir+json"
```

---

### GET /fhir/r4/Patient

Tìm kiếm Patient resources theo các FHIR search parameters.

- **Auth**: Bearer JWT
- **Permission**: `patients.view`

**Query Parameters**:

| Param | Type | Default | Description |
|---|---|---|---|
| name | string | null | Tìm kiếm theo tên (partial match trên first name, last name, middle name) |
| identifier | token | null | Tìm kiếm theo identifier (His.Hope internal ID) |
| birthdate | date | null | Tìm kiếm theo ngày sinh chính xác (định dạng yyyy-MM-dd) |

**Response** (200 OK) - Searchset Bundle:

```json
{
  "resourceType": "Bundle",
  "type": "searchset",
  "total": 1,
  "entry": [
    {
      "fullUrl": "https://fhir.his.hope.vn/fhir/r4/Patient/550e8400-e29b-41d4-a716-446655440000",
      "resource": {
        "resourceType": "Patient",
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "name": [
          {
            "use": "official",
            "family": "Nguyễn",
            "given": ["Hương", "Thị"]
          }
        ],
        "gender": "female",
        "birthDate": "1985-03-15",
        "active": true
      },
      "search": {
        "mode": "match"
      }
    }
  ]
}
```

**cURL Examples**:

```bash
# Tìm theo tên
curl -k -X GET "https://fhir.his.hope.vn/fhir/r4/Patient?name=Nguy%E1%BB%85n" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/fhir+json"

# Tìm theo identifier
curl -k -X GET "https://fhir.his.hope.vn/fhir/r4/Patient?identifier=550e8400-e29b-41d4-a716-446655440000" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/fhir+json"

# Tìm theo birthdate
curl -k -X GET "https://fhir.his.hope.vn/fhir/r4/Patient?birthdate=1985-03-15" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/fhir+json"

# Kết hợp nhiều parameters
curl -k -X GET "https://fhir.his.hope.vn/fhir/r4/Patient?name=Nguy%E1%BB%85n&birthdate=1985-03-15" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/fhir+json"
```

---

## Encounter Endpoints

### GET /fhir/r4/Encounter/{id}

Truy xuất một Encounter resource theo His.Hope internal ID.

- **Auth**: Bearer JWT
- **Permission**: `clinical.view`

**Path Parameters**:

| Param | Type | Description |
|---|---|---|
| id | string | His.Hope encounter UUID |

**Response** (200 OK):

```json
{
  "resourceType": "Encounter",
  "id": "990e8400-e29b-41d4-a716-446655440004",
  "identifier": [
    {
      "system": "https://his.hope.vn/encounter-id",
      "value": "990e8400-e29b-41d4-a716-446655440004"
    }
  ],
  "status": "in-progress",
  "class": {
    "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
    "code": "AMB",
    "display": "khám ngoại trú"
  },
  "subject": {
    "reference": "Patient/550e8400-e29b-41d4-a716-446655440000"
  },
  "period": {
    "start": "2026-07-16T08:30:00+07:00"
  }
}
```

**Status Codes**:

| Code | Description |
|---|---|
| 200 | Thành công |
| 401 | Token không hợp lệ hoặc hết hạn |
| 404 | Không tìm thấy Encounter với ID đã cho |

**cURL Example**:

```bash
curl -k -X GET "https://fhir.his.hope.vn/fhir/r4/Encounter/990e8400-e29b-41d4-a716-446655440004" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/fhir+json"
```

---

## FHIR Resource Mapping

### Patient Mapping

| His.Hope Field | FHIR Path | Notes |
|---|---|---|
| Patient.Id | `Patient.id` | Logical ID |
| Patient.Id | `Patient.identifier[0].value` | System: `https://his.hope.vn/patient-id`, Type: MR (Medical Record Number) |
| LastName + FirstName + MiddleName | `Patient.name[0].family` + `.given[]` | Use: official |
| GenderCode (M/F/O) | `Patient.gender` | M → male, F → female, O → other, UNKNOWN → unknown |
| DateOfBirth | `Patient.birthDate` | Format: yyyy-MM-dd |
| Phone | `Patient.telecom[]` | System: phone |
| Email | `Patient.telecom[]` | System: email |
| IsActive | `Patient.active` | Boolean |

### Encounter Mapping

| His.Hope Field | FHIR Path | Notes |
|---|---|---|
| Encounter.Id | `Encounter.id` | Logical ID |
| Encounter.Id | `Encounter.identifier[0].value` | System: `https://his.hope.vn/encounter-id` |
| StatusCode | `Encounter.status` | PLANNED/SCHEDULED → planned, IN_PROGRESS/ACTIVE → in-progress, ON_HOLD/PAUSED → onleave, DISCHARGED/FINISHED/COMPLETED → finished, CANCELLED/CANCELED → cancelled |
| TypeCode | `Encounter.class.code` | System: `http://terminology.hl7.org/CodeSystem/v3-ActCode` |
| PatientId | `Encounter.subject.reference` | Format: `Patient/{id}` |
| StartTime | `Encounter.period.start` | ISO 8601 with timezone |
| EndTime | `Encounter.period.end` | ISO 8601 with timezone (optional) |

---

## Status Codes và Error Handling

### FHIR Operation Outcome

Lỗi được trả về dưới dạng FHIR OperationOutcome resource:

```json
{
  "resourceType": "OperationOutcome",
  "issue": [
    {
      "severity": "error",
      "code": "not-found",
      "details": {
        "text": "Không tìm thấy Patient với ID: 550e8400-e29b-41d4-a716-446655440000"
      }
    }
  ]
}
```

### HTTP Status Codes

| Code | Description | FHIR Issue Code |
|---|---|---|
| 200 | Thành công | — |
| 400 | Bad Request | `invalid` |
| 401 | Unauthorized (token missing/invalid) | `login` |
| 403 | Forbidden (thiếu permission) | `forbidden` |
| 404 | Resource không tìm thấy | `not-found` |
| 422 | Dữ liệu không hợp lệ | `invariant` |
| 429 | Rate limit exceeded | `throttled` |
| 500 | Internal Server Error | `exception` |

---

## Future Endpoints (Roadmap)

Các endpoint FHIR sẽ được bổ sung trong các phiên bản sau:

| Resource | Method | Dự kiến |
|---|---|---|
| `Patient` | `POST` (create) | Q3 2026 |
| `Patient` | `PUT` (update) | Q3 2026 |
| `Encounter` | `GET /fhir/r4/Encounter` (search) | Q3 2026 |
| `Encounter` | `POST` (create) | Q4 2026 |
| `Practitioner` | CRUD | Q4 2026 |
| `Organization` | CRUD | Q4 2026 |
| `Observation` | Read + Search | Q1 2027 |
| `Condition` | Read + Search | Q1 2027 |
| `MedicationRequest` | Read + Search | Q1 2027 |
