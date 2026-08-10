# Agent Knowledge Base Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a structured knowledge base at `docs/knowledge/` with YAML-frontmatter entries, auto-generated INDEX.md, and .capture/ staging area — enabling agents to quickly look up gotchas, patterns, and decisions by tag instead of re-exploring the entire project.

**Architecture:** Markdown files organized by domain under `entries/`, each with YAML frontmatter for machine-parseable metadata. A PowerShell script generates INDEX.md with three lookup tables (tag, domain, severity). The `.capture/` directory serves as a staging area for agent-proposed entries pending human review.

**Tech Stack:** Markdown + YAML frontmatter, PowerShell 7, Git

## Global Constraints

- All paths under `docs/knowledge/`
- Naming convention: `{domain}-{type}-{nn}-{slug}.md` (e.g., `patient-gotcha-01-deadlock.md`)
- Every entry must have valid YAML frontmatter with: id, type, domain, tags (min 2), severity, agent, author, date
- INDEX.md is auto-generated via script — never edited by hand
- `.capture/` entries have extra fields: `status: pending-review`, `reviewer: @architect`
- 13 domain directories match existing services + agent domains

---
```

---

### Task 1: Create directory scaffold

**Files:**
- Create: `docs/knowledge/.capture/.gitkeep`
- Create: `docs/knowledge/entries/patient-service/.gitkeep`
- Create: `docs/knowledge/entries/identity-service/.gitkeep`
- Create: `docs/knowledge/entries/clinical-service/.gitkeep`
- Create: `docs/knowledge/entries/appointment-service/.gitkeep`
- Create: `docs/knowledge/entries/lab-service/.gitkeep`
- Create: `docs/knowledge/entries/billing-service/.gitkeep`
- Create: `docs/knowledge/entries/pharmacy-service/.gitkeep`
- Create: `docs/knowledge/entries/frontend/.gitkeep`
- Create: `docs/knowledge/entries/devops/.gitkeep`
- Create: `docs/knowledge/entries/security/.gitkeep`
- Create: `docs/knowledge/entries/database/.gitkeep`
- Create: `docs/knowledge/entries/ml-ai/.gitkeep`
- Create: `docs/knowledge/entries/data-platform/.gitkeep`
- Create: `docs/knowledge/scripts/.gitkeep`

**Interfaces:**
- Produces: 15 directories for subsequent tasks to populate

- [ ] **Step 1: Create all directories**

```powershell
$dirs = @(
    'docs/knowledge/.capture',
    'docs/knowledge/scripts',
    'docs/knowledge/entries/patient-service',
    'docs/knowledge/entries/identity-service',
    'docs/knowledge/entries/clinical-service',
    'docs/knowledge/entries/appointment-service',
    'docs/knowledge/entries/lab-service',
    'docs/knowledge/entries/billing-service',
    'docs/knowledge/entries/pharmacy-service',
    'docs/knowledge/entries/frontend',
    'docs/knowledge/entries/devops',
    'docs/knowledge/entries/security',
    'docs/knowledge/entries/database',
    'docs/knowledge/entries/ml-ai',
    'docs/knowledge/entries/data-platform'
)
foreach ($d in $dirs) {
    New-Item -ItemType Directory -Path $d -Force | Out-Null
    New-Item -ItemType File -Path "$d/.gitkeep" -Force | Out-Null
}
Write-Host "Created $($dirs.Count) directories"
```

Run: `pwsh -Command "$dirs = @(...); foreach ($d in $dirs) { New-Item -ItemType Directory -Path $d -Force | Out-Null; New-Item -ItemType File -Path "$d/.gitkeep" -Force | Out-Null }"`

Expected: 15 directories created, each with `.gitkeep`

- [ ] **Step 2: Verify structure**

Run: `Get-ChildItem -Recurse -Directory docs/knowledge | Select-Object FullName`

Expected: All 15 directories listed

- [ ] **Step 3: Commit**

```bash
git add docs/knowledge/
git commit -m "chore(knowledge): scaffold knowledge base directory structure"
```

---

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

Write to: `docs/knowledge/TEMPLATE.md`

- [ ] **Step 2: Commit**

```bash
git add docs/knowledge/TEMPLATE.md
git commit -m "docs(knowledge): add entry template with gotcha/pattern/decision guidelines"
```

---

### Task 3: Create generate-index.ps1 script

**Files:**
- Create: `docs/knowledge/scripts/generate-index.ps1`

**Interfaces:**
- Consumes: `.md` files under `entries/` with valid YAML frontmatter
- Produces: `docs/knowledge/INDEX.md`

- [ ] **Step 1: Write the script**

```powershell
# docs/knowledge/scripts/generate-index.ps1
<#
.SYNOPSIS
    Generate INDEX.md from knowledge base entries.
.DESCRIPTION
    Scans all .md files under entries/, parses YAML frontmatter,
    and generates INDEX.md with three lookup tables: by tag, by domain, by severity.
#>
param(
    [string]$KnowledgeDir = (Resolve-Path "$PSScriptRoot/..").Path
)

$ErrorActionPreference = 'Stop'
$entriesDir = Join-Path $KnowledgeDir 'entries'
$indexFile = Join-Path $KnowledgeDir 'INDEX.md'
$entries = [System.Collections.ArrayList]::new()

# --- 1. Scan all .md files in entries/ ---
$mdFiles = Get-ChildItem -Path $entriesDir -Recurse -Filter '*.md' -ErrorAction SilentlyContinue
if (-not $mdFiles) {
    Write-Warning "No .md files found in $entriesDir"
    return
}

foreach ($file in $mdFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    # Parse YAML frontmatter between first two --- blocks
    if ($content -notmatch '(?ms)^---\s*\n(.*?)\n---') { continue }
    $yamlBlock = $matches[1]

    # Extract fields
    $getId       = { param($y) if ($y -match '^id:\s*(.+)$') { $matches[1].Trim() } else { $null } }
    $getType     = { param($y) if ($y -match '^type:\s*(.+)$') { $matches[1].Trim() } else { $null } }
    $getDomain   = { param($y) if ($y -match '^domain:\s*(.+)$') { $matches[1].Trim() } else { $null } }
    $getSeverity = { param($y) if ($y -match '^severity:\s*(.+)$') { $matches[1].Trim() } else { 'info' } }

    $id       = & $getId $yamlBlock
    $type     = & $getType $yamlBlock
    $domain   = & $getDomain $yamlBlock
    $severity = & $getSeverity $yamlBlock

    if (-not $id -or -not $type -or -not $domain) {
        Write-Warning "Skipping $($file.Name): missing required frontmatter (id/type/domain)"
        continue
    }

    # Parse tags: supports [tag1, tag2] and inline YAML lists
    $tags = @()
    if ($yamlBlock -match '^tags:\s*\[(.+?)\]') {
        $tags = $matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }

    # Compute relative path from knowledge dir
    $relPath = $file.FullName.Replace($KnowledgeDir, '').TrimStart('\', '/') -replace '\\', '/'

    $null = $entries.Add(@{
        Id       = $id
        Type     = $type
        Domain   = $domain
        Tags     = $tags
        Severity = $severity
        RelPath  = $relPath
    })
}

if ($entries.Count -eq 0) {
    Write-Warning "No valid entries found"
    return
}

# --- 2. Build tag map ---
$tagMap = @{}
foreach ($e in $entries) {
    foreach ($t in $e.Tags) {
        if (-not $tagMap.ContainsKey($t)) { $tagMap[$t] = [System.Collections.ArrayList]::new() }
        $null = $tagMap[$t].Add($e)
    }
}

# --- 3. Build domain map ---
$domainMap = @{}
foreach ($e in $entries) {
    if (-not $domainMap.ContainsKey($e.Domain)) {
        $domainMap[$e.Domain] = @{ gotcha = @(); pattern = @(); decision = @() }
    }
    $null = $domainMap[$e.Domain][$e.Type].Add($e)
}

# --- 4. Build severity map ---
$severityMap = @{ critical = @(); warning = @(); info = @() }
foreach ($e in $entries) {
    $sev = $e.Severity
    if (-not $severityMap.ContainsKey($sev)) { $sev = 'info' }
    $null = $severityMap[$sev].Add($e)
}

# --- 5. Generate INDEX.md ---
$now = Get-Date -Format 'yyyy-MM-dd HH:mm'
$count = $entries.Count

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('# His.Hope Knowledge Base Index')
[void]$sb.AppendLine("> Auto-generated: $now | $count entries | Run ``./scripts/generate-index.ps1`` to update")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Quick Reference')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| What | Where |')
[void]$sb.AppendLine('|---|---|')
[void]$sb.AppendLine('| Gotchas (pitfalls to avoid) | Search by tag → read entry |')
[void]$sb.AppendLine('| Patterns (proven code templates) | Search by domain → read entry |')
[void]$sb.AppendLine('| Decisions (architectural choices) | Search by tag → read entry |')
[void]$sb.AppendLine('| Contribute new entry | Create in `.capture/` → @architect review → move to `entries/` |')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## By Tag')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Tag | Entries |')
[void]$sb.AppendLine('|---|---|')

foreach ($tag in ($tagMap.Keys | Sort-Object)) {
    $links = ($tagMap[$tag] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', '
    [void]$sb.AppendLine("| `` $tag `` | $links |")
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## By Domain')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Domain | Gotchas | Patterns | Decisions |')
[void]$sb.AppendLine('|---|---|---|---|')

foreach ($domain in ($domainMap.Keys | Sort-Object)) {
    $dm = $domainMap[$domain]
    $gItems = ($dm['gotcha'] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', '
    $pItems = ($dm['pattern'] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', '
    $dItems = ($dm['decision'] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', '
    if (-not $gItems) { $gItems = '—' }
    if (-not $pItems) { $pItems = '—' }
    if (-not $dItems) { $dItems = '—' }
    [void]$sb.AppendLine("| `` $domain `` | $gItems | $pItems | $dItems |")
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## By Severity')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Severity | Entries |')
[void]$sb.AppendLine('|---|---|')

$sevIcons = @{ critical = '🔴'; warning = '🟡'; info = '🔵' }
foreach ($sev in @('critical', 'warning', 'info')) {
    $items = $severityMap[$sev]
    if ($items.Count -gt 0) {
        $links = ($items | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', '
        $icon = $sevIcons[$sev]
        [void]$sb.AppendLine("| $icon $sev | $links |")
    }
}

# Write file
$sb.ToString() | Set-Content -Path $indexFile -Encoding UTF8

Write-Host "✅ INDEX.md generated: $count entries, $($tagMap.Count) tags, $($domainMap.Count) domains"
```

Write to: `docs/knowledge/scripts/generate-index.ps1`

- [ ] **Step 2: Test script with empty entries directory**

```powershell
pwsh -File docs/knowledge/scripts/generate-index.ps1
```

Expected: Warning "No .md files found" — exits gracefully

- [ ] **Step 3: Commit**

```bash
git add docs/knowledge/scripts/generate-index.ps1
git commit -m "feat(knowledge): add generate-index.ps1 script for auto-generating INDEX.md"
```

---

### Task 4: Create seed entries from existing knowledge

**Files:**
- Create: `docs/knowledge/entries/patient-service/patient-gotcha-01-deadlock.md`
- Create: `docs/knowledge/entries/patient-service/patient-pattern-01-aggregate-factory.md`
- Create: `docs/knowledge/entries/database/db-gotcha-01-backward-migration.md`
- Create: `docs/knowledge/entries/frontend/fe-gotcha-01-onpush-cdr.md`
- Create: `docs/knowledge/entries/security/security-gotcha-01-hardcoded-secrets.md`
- Create: `docs/knowledge/entries/devops/devops-decision-01-linkerd.md`

**Interfaces:**
- Consumes: knowledge from `docs/development/coding-standards.md`, `docs/adr/003-linkerd-over-istio.md`
- Produces: 6 seed entries for INDEX.md generation

- [ ] **Step 1: Write patient-gotcha-01-deadlock.md**

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

Write to: `docs/knowledge/entries/patient-service/patient-gotcha-01-deadlock.md`

- [ ] **Step 2: Write patient-pattern-01-aggregate-factory.md**

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

Write to: `docs/knowledge/entries/patient-service/patient-pattern-01-aggregate-factory.md`

- [ ] **Step 3: Write db-gotcha-01-backward-migration.md**

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

Write to: `docs/knowledge/entries/database/db-gotcha-01-backward-migration.md`

- [ ] **Step 4: Write fe-gotcha-01-onpush-cdr.md**

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

Write to: `docs/knowledge/entries/frontend/fe-gotcha-01-onpush-cdr.md`

- [ ] **Step 5: Write security-gotcha-01-hardcoded-secrets.md**

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

Write to: `docs/knowledge/entries/security/security-gotcha-01-hardcoded-secrets.md`

- [ ] **Step 6: Write devops-decision-01-linkerd.md**

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

Write to: `docs/knowledge/entries/devops/devops-decision-01-linkerd.md`

- [ ] **Step 7: Generate INDEX.md from seed entries**

```powershell
pwsh -File docs/knowledge/scripts/generate-index.ps1
```

Expected: `✅ INDEX.md generated: 6 entries, N tags, N domains`

- [ ] **Step 8: Verify INDEX.md content**

Read `docs/knowledge/INDEX.md` and check:
- Tag table has entries for: `deadlock`, `async`, `aggregate`, `migration`, `onpush`, `secrets`, `linkerd`
- Domain table has: `database`, `devops`, `frontend`, `patient-service`, `security`
- Severity table has: 3 critical, 1 warning, 2 info

- [ ] **Step 9: Commit**

```bash
git add docs/knowledge/entries/ docs/knowledge/INDEX.md
git commit -m "docs(knowledge): add 6 seed entries from existing ADRs and coding standards"
```

---

### Task 5: Update opencode.json

**Files:**
- Modify: `opencode.json`

**Interfaces:**
- Modifies: references object (add `knowledge`), agents.docs.description, agents.validate.description

- [ ] **Step 1: Read current opencode.json**

Read `opencode.json` — confirm current structure of references and agent descriptions.

- [ ] **Step 2: Add knowledge reference**

Add after the `"cockroach"` reference (last in the references object):

```json
"knowledge": {
  "path": "docs/knowledge",
  "description": "Use for agent knowledge base: gotchas, patterns, decisions"
}
```

- [ ] **Step 3: Update @docs agent description**

Change the description for the `docs` agent:

From:
```
"description": "Documentation agent — ADRs, API docs, service READMEs, changelogs, runbooks, dev guides. Runs in Phase 2 (generate) + Phase 4 (verify) of pipeline. Uses MCP: filesystem for reading/writing docs, github for git operations on doc files.",
```
To:
```
"description": "Documentation agent — ADRs, API docs, service READMEs, changelogs, runbooks, dev guides, knowledge base (capture & verify). Runs in Phase 2 (generate) + Phase 4 (verify) of pipeline. Uses MCP: filesystem for reading/writing docs, github for git operations on doc files.",
```

- [ ] **Step 4: Update @validate agent description**

Change the description for the `validate` agent:

From:
```
"description": "Validation agent (API contract, schema, FluentValidation, build, config/secrets, migration safety). Uses MCP: db-* for migration verification, docker for container validation, filesystem for config inspection.",
```
To:
```
"description": "Validation agent (API contract, schema, FluentValidation, build, config/secrets, migration safety, knowledge index freshness). Uses MCP: db-* for migration verification, docker for container validation, filesystem for config inspection.",
```

- [ ] **Step 5: Validate JSON syntax**

```powershell
Get-Content opencode.json | ConvertFrom-Json | Out-Null
Write-Host "✅ JSON valid"
```

Expected: "✅ JSON valid" — no parse errors

- [ ] **Step 6: Commit**

```bash
git add opencode.json
git commit -m "feat(knowledge): integrate knowledge base into opencode.json references and agent descriptions"
```

---

### Task 6: Final verification

**Files:**
- None (verification only)

- [ ] **Step 1: Verify complete file tree**

```powershell
Get-ChildItem -Recurse docs/knowledge -File | ForEach-Object { $_.FullName.Replace((Get-Location).Path + '\', '') }
```

Expected output includes:
```
docs/knowledge/INDEX.md
docs/knowledge/TEMPLATE.md
docs/knowledge/scripts/generate-index.ps1
docs/knowledge/.capture/.gitkeep
docs/knowledge/entries/patient-service/patient-gotcha-01-deadlock.md
docs/knowledge/entries/patient-service/patient-pattern-01-aggregate-factory.md
docs/knowledge/entries/database/db-gotcha-01-backward-migration.md
docs/knowledge/entries/frontend/fe-gotcha-01-onpush-cdr.md
docs/knowledge/entries/security/security-gotcha-01-hardcoded-secrets.md
docs/knowledge/entries/devops/devops-decision-01-linkerd.md
```

- [ ] **Step 2: Re-generate INDEX.md to confirm idempotent**

```powershell
pwsh -File docs/knowledge/scripts/generate-index.ps1
```

Expected: Same output — "✅ INDEX.md generated: 6 entries"

- [ ] **Step 3: Final commit (if any changes)**

```bash
git status
# Should show clean working tree
```
```

---

## Self-Review

**1. Spec coverage:**
- Section 3 (File Structure): Task 1 creates all directories ✅
- Section 4 (Entry Format): Task 4 creates entries following schema ✅
- Section 5 (INDEX.md): Task 3 (script) + Task 4 Step 7 (generate) ✅
- Section 6 (Agent Workflows): Documented in TEMPLATE.md ✅
- Section 7 (Pipeline Integration): Task 5 (opencode.json changes) ✅
- Section 8 (opencode.json): Task 5 ✅
- Section 9 (Script): Task 3 ✅
- Section 10 (Rollout Phase 1-3): Covered by Tasks 1-5 ✅

**2. Placeholder scan:** No TBD, TODO, or vague instructions. All code is concrete. ✅

**3. Type consistency:**
- `generate-index.ps1` output matches INDEX.md format in spec ✅
- Frontmatter fields match between TEMPLATE.md and actual entries ✅
- Entry IDs match filenames (patient-gotcha-01 ↔ patient-gotcha-01-deadlock.md) ✅
