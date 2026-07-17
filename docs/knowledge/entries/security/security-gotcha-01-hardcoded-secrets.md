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
