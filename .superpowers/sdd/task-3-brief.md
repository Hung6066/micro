### Task 3: Create generate-index.ps1 script

**Files:**
- Create: `docs/knowledge/scripts/generate-index.ps1`

**Interfaces:**
- Consumes: `.md` files under `entries/` with valid YAML frontmatter
- Produces: `docs/knowledge/INDEX.md`

**Global constraints from the spec:**
- INDEX.md is auto-generated via script — never edited by hand
- INDEX.md has exactly three lookup tables: By Tag, By Domain, By Severity
- Script must handle empty entries directory gracefully (warn, don't crash)
- Script must skip .md files with missing/invalid frontmatter (warn, continue)

- [ ] **Step 1: Write the script**

Create file at `docs/knowledge/scripts/generate-index.ps1`:

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
    Write-Output "# His.Hope Knowledge Base Index`n> No entries yet. Create entries in entries/{domain}/ then re-run this script.`n" | Set-Content -Path $indexFile -Encoding UTF8
    exit 0
}

foreach ($file in $mdFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    # Parse YAML frontmatter between first two --- blocks
    if ($content -notmatch '(?ms)^---\s*\n(.*?)\n---') { continue }
    $yamlBlock = $matches[1]

    # Extract fields using simple regex (avoids YAML library dependency)
    $id       = if ($yamlBlock -match '^id:\s*(.+)$') { $matches[1].Trim() } else { $null }
    $type     = if ($yamlBlock -match '^type:\s*(.+)$') { $matches[1].Trim() } else { $null }
    $domain   = if ($yamlBlock -match '^domain:\s*(.+)$') { $matches[1].Trim() } else { $null }
    $severity = if ($yamlBlock -match '^severity:\s*(.+)$') { $matches[1].Trim() } else { 'info' }

    if (-not $id -or -not $type -or -not $domain) {
        Write-Warning "Skipping $($file.Name): missing required frontmatter (id/type/domain)"
        continue
    }

    # Parse tags: supports [tag1, tag2] format
    $tags = @()
    if ($yamlBlock -match '^tags:\s*\[(.+?)\]') {
        $tags = $matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    }

    # Compute relative path from knowledge dir for links in INDEX.md
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
    Write-Warning "No valid entries found (all .md files skipped due to missing frontmatter)"
    Write-Output "# His.Hope Knowledge Base Index`n> No valid entries found. Check frontmatter in entries/*.md files.`n" | Set-Content -Path $indexFile -Encoding UTF8
    exit 0
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
        $domainMap[$e.Domain] = @{ gotcha = [System.Collections.ArrayList]::new(); pattern = [System.Collections.ArrayList]::new(); decision = [System.Collections.ArrayList]::new() }
    }
    $null = $domainMap[$e.Domain][$e.Type].Add($e)
}

# --- 4. Build severity map ---
$severityMap = @{ critical = [System.Collections.ArrayList]::new(); warning = [System.Collections.ArrayList]::new(); info = [System.Collections.ArrayList]::new() }
foreach ($e in $entries) {
    $sev = $e.Severity
    if (-not $severityMap.ContainsKey($sev)) { $sev = 'info' }
    $null = $severityMap[$sev].Add($e)
}

# --- 5. Generate INDEX.md content ---
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
[void]$sb.AppendLine('| Gotchas (pitfalls to avoid) | Search by tag below → read entry |')
[void]$sb.AppendLine('| Patterns (proven code templates) | Search by domain below → read entry |')
[void]$sb.AppendLine('| Decisions (architectural choices) | Search by tag below → read entry |')
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
    $gItems = if ($dm['gotcha'].Count -gt 0) { ($dm['gotcha'] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', ' } else { '—' }
    $pItems = if ($dm['pattern'].Count -gt 0) { ($dm['pattern'] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', ' } else { '—' }
    $dItems = if ($dm['decision'].Count -gt 0) { ($dm['decision'] | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', ' } else { '—' }
    [void]$sb.AppendLine("| `` $domain `` | $gItems | $pItems | $dItems |")
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine('---')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## By Severity')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Severity | Entries |')
[void]$sb.AppendLine('|---|---|')

$sevIcons = @{ critical = 'critical'; warning = 'warning'; info = 'info' }
foreach ($sev in @('critical', 'warning', 'info')) {
    $items = $severityMap[$sev]
    if ($items.Count -gt 0) {
        $links = ($items | ForEach-Object { "[$($_.Id)]($($_.RelPath))" }) -join ', '
        [void]$sb.AppendLine("| $sev | $links |")
    }
}

# Write to file
$sb.ToString() | Set-Content -Path $indexFile -Encoding UTF8

Write-Host "INDEX.md generated: $count entries, $($tagMap.Count) tags, $($domainMap.Count) domains"
```

- [ ] **Step 2: Test script with empty entries directory (should warn gracefully)**

```powershell
pwsh -File docs/knowledge/scripts/generate-index.ps1
```

Expected: Warning about no .md files, creates minimal INDEX.md

- [ ] **Step 3: Commit**

```bash
git add docs/knowledge/scripts/generate-index.ps1
git commit -m "feat(knowledge): add generate-index.ps1 script for auto-generating INDEX.md"
```
