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

- [ ] **Step 2: Verify structure**

Run: `Get-ChildItem -Recurse -Directory docs/knowledge | Select-Object FullName`

Expected: All 15 directories listed

- [ ] **Step 3: Commit**

```bash
git add docs/knowledge/
git commit -m "chore(knowledge): scaffold knowledge base directory structure"
```
