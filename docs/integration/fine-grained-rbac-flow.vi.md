# Fine-grained RBAC — kiến trúc, flow và vận hành

## 1. Mục tiêu và nguyên tắc

Fine-grained RBAC của His.Hope dùng RBAC làm entitlement nền, sau đó thêm
resource-aware checks và facility/tenant scope ở chính service sở hữu dữ liệu.
Frontend chỉ điều khiển affordance; quyết định server-side là security boundary.

Các nguyên tắc bắt buộc:

- deny-by-default và fail-closed;
- không tin `facilityId`, tenant hoặc resource metadata từ request body;
- service sở hữu resource phải load metadata trước khi mutation/read nhạy cảm;
- cross-facility denial dùng response không tiết lộ resource (`404` ở domain API);
- mọi decision có audit metadata đã redaction, không ghi PHI, token hay canonical id;
- human và workload principal tách biệt; workload không kế thừa quyền admin tương tác;
- P2 shadow/canary chỉ quan sát, không bao giờ tự grant.

## 2. Mô hình quyền

```mermaid
flowchart LR
    H[Human principal] --> RBAC[Permission catalog / roles]
    W[Workload principal] --> SCOPES[OAuth client-credentials scopes]
    RBAC --> PEP[Service PEP]
    SCOPES --> PEP
    FAC[Facility / tenant claims] --> PEP
    RES[Trusted resource metadata] --> PEP
    POST[Device posture / purpose context] --> PEP
    PEP --> DEC{Allow?}
    DEC -->|Allow| ACT[Execute handler or query]
    DEC -->|Deny| SAFE[401/403/404 non-enumerating]
    PEP --> AUD[Redacted decision audit]
    PEP -. advisory .-> SHADOW[P2 shadow/canary probe]
    SHADOW -. never grants .-> DEC
```

### Authorization context

`AuthorizationContext` gồm principal, action, trusted `AuthorizationResource`,
purpose/device/emergency context và các cờ `RequireResource`/
`ResourceLookupFailed`. Resource gồm type, canonical id, tenant, facility,
sensitivity và lifecycle state.

`AuthorizationEvaluator` xử lý theo thứ tự:

1. principal đã authenticated chưa;
2. action có hợp lệ không;
3. resource bắt buộc có tồn tại không;
4. resource lookup có thất bại không;
5. principal có permission/action không;
6. principal có facility scope không;
7. nếu tất cả pass thì allow.

```mermaid
flowchart TD
    START[Request vào service] --> AUTH{Authenticated?}
    AUTH -->|No| D1[Deny unauthenticated]
    AUTH -->|Yes| ACTION{Action hợp lệ?}
    ACTION -->|No| D2[Deny invalid_action]
    ACTION -->|Yes| NEED{Require resource?}
    NEED -->|Yes| LOAD[Service load resource metadata bằng id]
    LOAD --> FOUND{Có resource?}
    FOUND -->|No| D3[Deny resource_not_found]
    FOUND -->|Yes| PERM{Permission/action có?}
    NEED -->|No| PERM
    PERM -->|No| D4[Deny permission_missing]
    PERM -->|Yes| FAC{Facility scope hợp lệ?}
    FAC -->|No| D5[Deny facility_scope_denied]
    FAC -->|Yes| ALLOW[Allow]
    D1 --> AUD[Audit redacted]
    D2 --> AUD
    D3 --> AUD
    D4 --> AUD
    D5 --> AUD
    ALLOW --> AUD
    ALLOW --> EXEC[Execute operation]
```

## 3. Request flow theo loại operation

### Read-by-id

```mermaid
sequenceDiagram
    participant C as Client/BFF
    participant S as Domain service
    participant E as Shared evaluator
    participant DB as Domain DB
    participant A as Audit sink

    C->>S: GET /resource/{id} + bearer token
    S->>DB: Load trusted facility/resource metadata by id
    DB-->>S: metadata or no row
    S->>E: Evaluate(action, resource, facility)
    E->>A: decision metadata only
    alt allow
        E-->>S: Allow
        S->>DB: Query/project resource
        DB-->>S: resource DTO
        S-->>C: 200 DTO
    else deny or missing
        E-->>S: Deny
        S-->>C: 404 non-enumerating
    end
```

### Mutation

Mutation phải evaluate trước khi mediator/handler thay đổi dữ liệu. `facilityId`
từ body không được dùng để cấp quyền.

```mermaid
sequenceDiagram
    participant C as Client
    participant S as Service PEP
    participant E as Evaluator
    participant H as Command handler
    participant DB as Database

    C->>S: PUT/POST/DELETE resource id
    S->>E: Load resource + evaluate mutation action
    alt deny
        E-->>S: Deny
        S-->>C: 404/403
    else allow
        E-->>S: Allow
        S->>H: Execute command
        H->>DB: Transaction
        DB-->>H: Commit
        H-->>S: Result
        S-->>C: 200/202
    end
```

### List/search

List/search không load từng row để quyết định. Service tạo `FacilityAccessScope`
từ principal, truyền scope vào repository và partition cache key theo scope.
Scope rỗng với principal không có cross-facility access phải trả empty result.

## 4. Human và workload principal

```mermaid
flowchart LR
    LOGIN[Interactive login] --> HUMAN[principal_type=human]
    CC[OAuth2 client_credentials] --> WORKLOAD[principal_type=workload]
    HUMAN --> ADMIN[HumanAdmin policy + admin permissions]
    WORKLOAD --> INT[Explicit integration policy]
    INT --> SCIM[SCIM scopes]
    INT --> CONT[Continuity scope]
    ADMIN -. rejected .-> WORKLOAD
```

`HumanAdmin` áp dụng cho Identity admin surface. Workload token có admin
permission nhưng thiếu principal type human vẫn bị từ chối.

## 5. gRPC và service-to-service

gRPC methods áp dụng cùng evaluator trước repository/mediator access. Metadata
scope phải được truyền xuyên từ request context đến repository; không coi nội
bộ network là trusted boundary.

```mermaid
flowchart LR
    O[Caller service] --> JWT[JWT validation: issuer/audience]
    JWT --> PT{principal_type}
    PT -->|workload| SCOPE[Required workload scope]
    PT -->|human| PERM[Permission + facility scope]
    SCOPE --> G[ gRPC PEP ]
    PERM --> G
    G --> R[Resource evaluator]
    R -->|allow| Q[Scoped repository query]
    R -->|deny| NF[NotFound / permission denial]
```

## 6. P2 shadow/canary

P2 hiện là seam an toàn, chưa phải external PDP runtime. `AUTHZ_PDP_MODE`:

| Mode | Hành vi |
|---|---|
| `disabled` | Chỉ local P1 evaluator |
| `shadow` | Ghi coarse telemetry để so sánh PDP sau này; local decision authoritative |
| `canary` | Hiện vẫn non-granting, fail-closed như shadow |

```mermaid
flowchart TD
    REQ[Request] --> LOCAL[Local P1 evaluator]
    LOCAL --> DEC[Local decision]
    LOCAL -. mode shadow/canary .-> PDP[Future external PDP adapter]
    PDP --> CMP[Compare only]
    CMP --> TELEMETRY[Telemetry mismatch / latency]
    PDP -. timeout/error .-> FAIL[Ignore probe error]
    DEC --> OUT[Return local decision]
```

Không bật `stepup/deny` cho clinical production chỉ từ posture shadow. Promotion
P2 cần model tests `Check`/`ListObjects`, negative tenant/hierarchy cases,
timeout chaos, tuple reconciliation và rollback về P1.

## 7. Frontend foundation flow

```mermaid
flowchart LR
    SNAP[Server permission snapshot + authz_version] --> F[HisHopePermissionService]
    F --> GUARD[Route guard / capability guard]
    F --> BTN[Permission button / export affordance]
    GUARD -->|missing entitlement| DENY[Localized denial state]
    BTN -->|UX hide/disable| UX[Theme tokens + i18n]
    API[Actual API call] --> PEP[Server-side PEP]
    PEP -->|401/403| ERR[Error interceptor]
    ERR --> DENY
```

Frontend không được dùng để suy luận resource authorization. Guard/button chỉ
tối ưu UX; server vẫn kiểm tra permission, scope, resource và facility.

Các ứng dụng admin/dashboard dùng trực tiếp interceptor chung của foundation.
Clinical app còn giữ interceptor legacy vì có audit/snackbar/session behavior
riêng, nhưng interceptor này đã bridge 401/403 vào `HisHopePermissionService`
với cùng failure state; do đó không tạo thêm một permission model thứ hai.

## 8. Audit và observability

Decision audit nên chứa: `decisionId`, status, action, reason code, resource
type, subject hash/identifier theo chính sách, tenant/facility scope và
correlation id. Không ghi canonical resource id, PHI, raw claims, bearer token
hoặc policy internals.

Các metric nên theo dõi:

- allow/deny theo action/resource type/reason code;
- cross-facility denial và resource lookup failure;
- shadow mismatch rate, latency, timeout;
- stale permission snapshot và authz version mismatch;
- workload token scope/audience rejection.

## 9. Verification matrix

| Gate | Evidence hiện có |
|---|---|
| Shared evaluator | Authorization tests **26/26** |
| Domain application | **267/267** |
| HTTP read resource/facility | **12/12** |
| gRPC read resource/facility | **12/12** |
| Mutation cross-facility | **6/6** |
| FHIR direct boundary | **7/7** |
| SCIM workload boundary | **9/9** |
| Frontend foundation | Karma **54/54** |
| Dashboard | Karma **34/34** |
| Main frontend | Jest **73 suites / 480 tests** |
| Docker runtime | Compose smoke + live UI endpoints pass |
| K8s runtime | dev/staging/prod Kustomize validation pass |

Các gate chưa thể claim trong môi trường hiện tại: live client-credentials với
Vault/secret thật, external PDP/OpenFGA, chaos PDP/PIP, Google/Entra/SSF/mTLS/
RADIUS/Chrome/Windows vendor labs.

## 10. Rollout và rollback

1. Deploy với local P1 evaluator và `AUTHZ_PDP_MODE=disabled`.
2. Enable `shadow` ở staging, quan sát mismatch và không grant theo PDP.
3. Chỉ pilot bounded domain, có owner và rollback switch.
4. Nếu PDP lỗi hoặc mismatch tăng: chuyển `disabled`; quyền local P1 không đổi.
5. Chỉ production canary sau khi live gates, security review và clinical safety
   sign-off hoàn tất.
