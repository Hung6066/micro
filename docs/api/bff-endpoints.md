# BFF API Endpoints

## Overview

All BFF endpoints are served through the ApiGateway YARP edge router. The BFF layer handles session authentication via HttpOnly cookies and provides both pass-through proxy and data aggregation.

## Base URLs

| BFF | Internal | Public (via ApiGateway) |
|-----|----------|------------------------|
| PatientBff | `http://patient-bff.his-hope.svc:5100` | `http://api-gateway:5000/api/v1/bff/patients/*` |
| ClinicalBff | `http://clinical-bff.his-hope.svc:5200` | `http://api-gateway:5000/api/v1/bff/encounters/*` |
| LabBff | `http://lab-bff.his-hope.svc:5300` | `http://api-gateway:5000/api/v1/bff/lab/*` |
| BillingBff | `http://billing-bff.his-hope.svc:5400` | `http://api-gateway:5000/api/v1/bff/billing/*` |
| PharmacyBff | `http://pharmacy-bff.his-hope.svc:5500` | `http://api-gateway:5000/api/v1/bff/pharmacy/*` |
| DashboardBff | `http://dashboard-bff.his-hope.svc:5600` | `http://api-gateway:5000/api/v1/bff/dashboard/*` |

## Endpoints

### PatientBff — Proxy Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/bff/patients/{id}` | Get patient by ID |
| POST | `/api/v1/bff/patients` | Create patient |
| PUT | `/api/v1/bff/patients/{id}` | Update patient |
| DELETE | `/api/v1/bff/patients/{id}` | Delete patient |
| GET | `/api/v1/bff/patients/search` | Search patients |
| GET | `/api/v1/bff/patients/{id}/encounters` | Get patient encounters |

### PatientBff — Aggregation

| Method | Route | Description | Backing Services |
|--------|-------|-------------|-----------------|
| GET | `/api/v1/patients/{id}/timeline` | Patient timeline | Patient, Clinical, Lab, Pharmacy |

### ClinicalBff — Proxy Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/bff/encounters/{id}` | Get encounter |
| POST | `/api/v1/bff/encounters` | Create encounter |
| PUT | `/api/v1/bff/encounters/{id}` | Update encounter |
| GET | `/api/v1/bff/encounters/search` | Search encounters |

### ClinicalBff — Aggregation

| Method | Route | Description | Backing Services |
|--------|-------|-------------|-----------------|
| GET | `/api/v1/encounters/{id}/detailed` | Encounter with patient data | Clinical, Patient |
| GET | `/api/v1/encounters/stats` | Encounter statistics | Clinical |

### LabBff — Proxy Only

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/bff/lab/orders/{id}` | Get lab order |
| POST | `/api/v1/bff/lab/orders` | Create lab order |
| GET | `/api/v1/bff/lab/orders/search` | Search lab orders |
| GET | `/api/v1/bff/lab/orders/{id}/results` | Get lab order results |
| POST | `/api/v1/bff/lab/orders/{id}/results` | Add lab result |
| GET | `/api/v1/bff/lab/patients/{id}/orders` | Get patient lab orders |
| GET | `/api/v1/bff/lab/patients/{id}/critical-alerts` | Get patient critical alerts |

### BillingBff — Proxy Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/bff/billing/invoices/{id}` | Get invoice |
| GET | `/api/v1/bff/billing/invoices/search` | Search invoices |
| GET | `/api/v1/bff/billing/patients/{id}/invoices` | Get patient invoices |
| POST | `/api/v1/bff/billing/invoices` | Create invoice |
| GET | `/api/v1/bff/billing/payments/{id}` | Get payment |

### BillingBff — Aggregation

| Method | Route | Description | Backing Services |
|--------|-------|-------------|-----------------|
| GET | `/api/v1/invoices/{id}/detailed` | Invoice with line items and payments | Billing |

### PharmacyBff — Proxy Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/bff/pharmacy/medications/{id}` | Get medication |
| GET | `/api/v1/bff/pharmacy/medications/search` | Search medications |
| POST | `/api/v1/bff/pharmacy/medications` | Create medication |
| PUT | `/api/v1/bff/pharmacy/medications/{id}` | Update medication |
| GET | `/api/v1/bff/pharmacy/prescriptions/{id}` | Get prescription |
| GET | `/api/v1/bff/pharmacy/prescriptions/search` | Search prescriptions |
| POST | `/api/v1/bff/pharmacy/prescriptions` | Create prescription |
| GET | `/api/v1/bff/pharmacy/patients/{id}/prescriptions` | Get patient prescriptions |
| GET | `/api/v1/bff/pharmacy/patients/{id}/medications` | Get patient medications |

### PharmacyBff — Aggregation

| Method | Route | Description | Backing Services |
|--------|-------|-------------|-----------------|
| GET | `/api/v1/prescriptions/{id}/full` | Prescription with medication details | Pharmacy, Clinical |
| GET | `/api/v1/patients/{id}/medication-history` | Complete medication history | Pharmacy, Clinical |
| GET | `/api/v1/pharmacy/dashboard/summary` | Pharmacy dashboard summary | Pharmacy |

### DashboardBff — Aggregation Only

| Method | Route | Description | Backing Services |
|--------|-------|-------------|-----------------|
| GET | `/api/v1/dashboard/stats` | Dashboard statistics | Patient, Clinical, Lab, Billing, Pharmacy |
| GET | `/api/v1/dashboard/recent-encounters` | Recent encounters | Clinical |
| GET | `/api/v1/dashboard/upcoming-appointments` | Upcoming appointments | Appointment |

## Authentication

All BFF endpoints use session-based auth via HttpOnly cookie:

- **Cookie**: `hishop_sid` (session ID, stored in Redis)
- **CSRF**: `X-CSRF-Token` header required for POST/PUT/PATCH/DELETE
- **No JWT Bearer tokens**: The frontend no longer manages access tokens

## Response Format

### Success
```json
{
  "data": { ... },
  "degraded": []
}
```

### Partial Degradation
```json
{
  "data": { "stats": { "totalPatients": { "count": 1240 }, ... } },
  "degraded": [
    { "field": "pendingLabs", "reason": "Lab service unavailable", "correlationId": "..." }
  ]
}
```

### Failure
```json
{
  "data": { "error": "All downstream services unavailable" },
  "degraded": [...]
}
```
