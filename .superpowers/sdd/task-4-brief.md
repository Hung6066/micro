### Task 4: Create seed entries from existing knowledge

**Files to create (6 entries):**
1. `docs/knowledge/entries/patient-service/patient-gotcha-01-deadlock.md`
2. `docs/knowledge/entries/patient-service/patient-pattern-01-aggregate-factory.md`
3. `docs/knowledge/entries/database/db-gotcha-01-backward-migration.md`
4. `docs/knowledge/entries/frontend/fe-gotcha-01-onpush-cdr.md`
5. `docs/knowledge/entries/security/security-gotcha-01-hardcoded-secrets.md`
6. `docs/knowledge/entries/devops/devops-decision-01-linkerd.md`

**Interfaces:**
- Consumes: knowledge from `docs/development/coding-standards.md`, `docs/adr/003-linkerd-over-istio.md`
- Produces: 6 seed entries for INDEX.md generation

**Global constraints:**
- Every entry must have valid YAML frontmatter with: id, type, domain, tags (min 2), severity, agent, author, date
- Naming convention: `{domain}-{type}-{nn}-{slug}.md`
- 1 entry = 1 lesson
- Must include code examples (correct + incorrect for gotchas)

---

- [ ] **Step 1: Create patient-gotcha-01-deadlock.md**

Write to: `docs/knowledge/entries/patient-service/patient-gotcha-01-deadlock.md`

```markdown
---
id: patient-gotcha-01
type: gotcha
domain: patient-service
tags: [deadlock, async, ef-core, dotnet]
severity: critical
agent: @dotnet
author: @architect
date: 2026-07-17
related: []
---

# Không dùng .Result() hoặc .Wait() với async

## Vấn đề
Gọi `.Result` hoặc `.Wait()` trên Task trong ASP.NET Core gây deadlock do SynchronizationContext bị block. Đã xảy ra 2 lần trong PatientService và AppointmentService.

## Hậu quả
- Request treo vĩnh viễn, thread pool cạn kiệt
- Service cần restart để phục hồi
- Không có exception rõ ràng — chỉ thấy timeout

## Cách phát hiện
```bash
rg "\.Result|\.Wait\(\)" src/Services/ --include="*.cs"
```

## Cách làm đúng
```csharp
// ✅ ĐÚNG: async all the way down
public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken ct)
{
    var patient = Patient.Register(...);
    await _patientRepository.AddAsync(patient, ct);
    await _unitOfWork.SaveChangesAsync(ct);
    return _mapper.Map<PatientDto>(patient);
}

// ❌ SAI: block async — DEADLOCK RISK
var patient = _patientRepository.GetByIdAsync(id).Result;
var patient = _patientRepository.GetByIdAsync(id).Wait();

// ✅ Nếu bắt buộc sync (rất hiếm): dùng GetAwaiter().GetResult()
var patient = _patientRepository.GetByIdAsync(id).GetAwaiter().GetResult();
```

## Đã xảy ra ở đâu
- PatientService: `PatientRepository.GetByIdAsync` bị gọi .Result trong controller (đã fix tháng 3/2026)
- AppointmentService: `BookingHandler` (đã fix tháng 5/2026)
```

---

- [ ] **Step 2: Create patient-pattern-01-aggregate-factory.md**

Write to: `docs/knowledge/entries/patient-service/patient-pattern-01-aggregate-factory.md`

```markdown
---
id: patient-pattern-01
type: pattern
domain: patient-service
tags: [aggregate, factory-method, domain, dotnet]
severity: info
agent: @dotnet
author: @architect
date: 2026-07-17
related: []
---

# Aggregate Root với Static Factory Method

## Khi nào dùng
Mọi Aggregate Root trong Domain layer **phải** được tạo qua static factory method, không dùng constructor public. Áp dụng cho tất cả service: Patient, Clinical, Appointment, Lab, Billing, Pharmacy, Identity.

## Mẫu chuẩn
```csharp
public class Patient : AggregateRoot<PatientId>
{
    public PersonName Name { get; private set; }
    public DateTime DateOfBirth { get; private set; }
    public bool IsActive { get; private set; }

    // Constructor PRIVATE cho EF Core
    private Patient() { }

    // Factory method — luôn có validation + domain event
    public static Patient Register(
        PersonName name, DateTime dateOfBirth, Gender gender,
        ContactInfo contactInfo, Address address)
    {
        Guard.Against.Null(name, nameof(name));
        Guard.Against.Null(gender, nameof(gender));

        var id = PatientId.New();
        var age = CalculateAge(dateOfBirth);
        Guard.Against.BusinessRule(new PatientMustBeAtLeastZeroYearsOld(age));

        var patient = new Patient
        {
            Id = id,
            Name = name,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            ContactInfo = contactInfo,
            Address = address,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        patient.AddDomainEvent(new PatientRegisteredDomainEvent(id.Value, name.FullName));
        return patient;
    }
}
```

## Lý do
- Đảm bảo domain invariant luôn được kiểm tra khi tạo entity
- Domain event được raise nhất quán
- Constructor private ngăn việc tạo entity không hợp lệ từ bên ngoài

## Tham khảo
- `docs/development/coding-standards.md` section 1.3
- `src/Services/PatientService/PatientService.Domain/Aggregates/Patient.cs`
```

---

- [ ] **Step 3: Create db-gotcha-01-backward-migration.md**

Write to: `docs/knowledge/entries/database/db-gotcha-01-backward-migration.md`

```markdown
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
```

---

- [ ] **Step 4: Create fe-gotcha-01-onpush-cdr.md**

Write to: `docs/knowledge/entries/frontend/fe-gotcha-01-onpush-cdr.md`

```markdown
---
id: fe-gotcha-01
type: gotcha
domain: frontend
tags: [onpush, change-detection, angular, performance]
severity: warning
agent: @angular
author: @architect
date: 2026-07-17
related: []
---

# OnPush component không tự cập nhật nếu thiếu markForCheck()

## Vấn đề
Khi dùng `ChangeDetectionStrategy.OnPush`, Angular chỉ re-render component khi:
- Input reference thay đổi
- Event từ trong component
- Observable pipe với async
- **HOẶC** gọi `ChangeDetectorRef.markForCheck()` thủ công

Nếu quên gọi `markForCheck()` sau khi data thay đổi từ bên ngoài (service, store, subscription), UI sẽ không cập nhật.

## Hậu quả
- UI hiển thị dữ liệu cũ/stale
- Người dùng thấy thông tin sai
- Khó debug vì không có error

## Cách phát hiện
- Kiểm tra component có `OnPush` nhưng không có `markForCheck()` trong subscription callbacks
- Dấu hiệu: data load về từ API nhưng UI không hiển thị

## Cách làm đúng
```typescript
// ✅ ĐÚNG: gọi markForCheck() sau khi data thay đổi
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `...`,
})
export class PatientListComponent implements OnInit {
  patients: Patient[] = [];

  constructor(
    private patientService: PatientService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.patientService.search().subscribe(data => {
      this.patients = data;
      this.cdr.markForCheck();  // ← BẮT BUỘC với OnPush
    });
  }
}

// ❌ SAI: thiếu markForCheck() — UI không cập nhật
this.patientService.search().subscribe(data => {
  this.patients = data;
  // Thiếu: this.cdr.markForCheck();
});

// ✅ ALTERNATIVE: dùng async pipe (tự động markForCheck)
patients$ = this.patientService.search();
// Trong template: *ngFor="let p of patients$ | async"
```

## Đã xảy ra ở đâu
- PatientListComponent: load xong nhưng table trống (tháng 6/2026)
- ClinicalNotesComponent: note mới lưu không hiện ngay (tháng 7/2026)
```

---

- [ ] **Step 5: Create security-gotcha-01-hardcoded-secrets.md**

Write to: `docs/knowledge/entries/security/security-gotcha-01-hardcoded-secrets.md`

```markdown
---
id: security-gotcha-01
type: gotcha
domain: security
tags: [secrets, vault, dotnet, kubernetes]
severity: critical
agent: @security
author: @architect
date: 2026-07-17
related: []
---

# KHÔNG hardcode secrets trong code hoặc config

## Vấn đề
Connection string, API key, password bị hardcode trong `appsettings.json`, code C#, hoặc K8s manifest. Đây là lỗi bảo mật nghiêm trọng — secrets bị lộ qua git history.

## Hậu quả
- Lộ credentials production qua git history
- Vi phạm HIPAA Security Rule §164.312(a)(2)(i) — encryption of PHI at rest
- Audit finding: Critical severity

## Cách phát hiện
```bash
# Quét connection string pattern
rg "Server=|Host=|Password=|ConnectionString=" src/ --include="*.cs" --include="*.json"

# Quét API key pattern
rg "[a-zA-Z0-9_-]{20,}" src/ --include="*.json" | grep -v "node_modules"
```

## Cách làm đúng
```csharp
// ✅ ĐÚNG: Secrets từ Vault, inject qua IConfiguration
var connectionString = configuration.GetConnectionString("PatientDb");
// appsettings.json: "PatientDb": "" (empty in repo)
// Real value injected from Vault via K8s secret

// ✅ ĐÚNG: Development dùng User Secrets
// dotnet user-secrets set "ConnectionStrings:PatientDb" "Host=localhost;..."

// ❌ SAI: Hardcode trong code
var connStr = "Host=prod.db.com;Password=SuperSecret123!";

// ❌ SAI: Hardcode trong appsettings.json tracked by git
{ "ConnectionStrings": { "PatientDb": "Host=prod.db.com;Password=..." } }
```

## Vault policy mẫu
```hcl
# vault/policies/patient-service.hcl
path "secret/data/his-hope/patient-service/*" {
  capabilities = ["read", "list"]
}
path "secret/data/his-hope/database/patientdb" {
  capabilities = ["read"]
}
```
```

---

- [ ] **Step 6: Create devops-decision-01-linkerd.md**

Write to: `docs/knowledge/entries/devops/devops-decision-01-linkerd.md`

```markdown
---
id: devops-decision-01
type: decision
domain: devops
tags: [linkerd, service-mesh, istio, kubernetes]
severity: info
agent: @devops
author: @architect
date: 2026-07-17
related: [adr-003]
---

# Chọn Linkerd thay vì Istio làm service mesh

## Quyết định
Dùng Linkerd làm service mesh cho toàn bộ His.Hope cluster.

## Lý do
- Linkerd nhẹ hơn 10x về resource (50MB vs 500MB RAM mỗi pod sidecar)
- Cấu hình đơn giản — không cần CRDs phức tạp như Istio (VirtualService, DestinationRule, Gateway...)
- mTLS tự động giữa tất cả pod mà không cần cấu hình thủ công
- Phù hợp với team size hiện tại (không cần đội ngũ chuyên trách service mesh)

## Trade-off đã cân nhắc
| Tiêu chí | Linkerd | Istio |
|---|---|---|
| Resource footprint | ~50MB/pod | ~500MB/pod |
| Cấu hình | Tự động, ít can thiệp | CRDs phức tạp |
| mTLS | Tự động mesh-wide | Cần PeerAuthentication policy |
| Multi-cluster | Hạn chế (multi-cluster gateways) | Mạnh (Istio multi-cluster) |
| Ecosystem | Nhỏ hơn | CNCF graduated, ecosystem lớn |
| Observability | Tích hợp Prometheus + Grafana | Kiali, Jaeger, Grafana |

## Khi nào xem xét lại
- Khi cần multi-cluster mesh federation phức tạp (>3 clusters)
- Khi team có >5 SRE chuyên trách
- Khi cần advanced traffic routing (A/B testing, canary với header-based routing)

## Tham khảo
- `docs/adr/003-linkerd-over-istio.md`
- `docs/linkerd-guide.md`
```

---

- [ ] **Step 7: Generate INDEX.md from seed entries**

```powershell
pwsh -File docs/knowledge/scripts/generate-index.ps1
```

Expected: `INDEX.md generated: 6 entries, N tags, N domains`

- [ ] **Step 8: Commit**

```bash
git add docs/knowledge/entries/ docs/knowledge/INDEX.md
git commit -m "docs(knowledge): add 6 seed entries from existing ADRs and coding standards"
```
