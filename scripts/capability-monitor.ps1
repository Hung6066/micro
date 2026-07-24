<#
.SYNOPSIS
    Agent Capability Monitor — detects new agent capabilities from PR code output.
.DESCRIPTION
    Analyzes a PR diff for code patterns (NuGet packages, design patterns, APIs),
    compares against known capabilities in agent-capabilities.json, and creates
    a follow-up PR if new capabilities are detected.
.PARAMETER PrNumber
    GitHub PR number to analyze.
.PARAMETER AgentName
    Agent name (e.g., "@dotnet", "@angular").
.PARAMETER TriggerMode
    "auto" (default) — creates PR for capability updates.
    "manual" — only prints detection results, no PR.
.EXAMPLE
    .\capability-monitor.ps1 -PrNumber 342 -AgentName "@dotnet"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$PrNumber,

    [Parameter(Mandatory=$true)]
    [string]$AgentName,

    [ValidateSet("auto", "manual")]
    [string]$TriggerMode = "auto"
)

$ErrorActionPreference = "Stop"
$RepoRoot = (git rev-parse --show-toplevel)

function Get-PrDiff {
    param([string]$Pr)
    $diff = gh pr diff $Pr --color=never 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Failed to get PR diff for #$Pr" }
    return $diff
}

function Load-JsonFile {
    param([string]$Path)
    if (-not (Test-Path $Path)) { throw "File not found: $Path" }
    try {
        return Get-Content $Path -Raw | ConvertFrom-Json
    } catch {
        throw "Malformed JSON in $Path : $_"
    }
}

function Detect-Capabilities {
    param([string]$Diff, $Rules)
    $detected = @()
    foreach ($rule in $Rules.rules) {
        $matchCount = 0
        $matchedPatterns = @()
        foreach ($pattern in $rule.patterns) {
            if ($Diff -match $pattern) {
                $matchCount++
                $matchedPatterns += $pattern
            }
        }
        if ($matchCount -ge $rule.min_matches) {
            $detected += [PSCustomObject]@{
                id = $rule.id
                category = $rule.category
                description = $rule.description
                evidence = ($matchedPatterns -join ', ')
            }
        }
    }
    return $detected
}

function Compare-WithRegistry {
    param($Detected, $Registry, [string]$Agent)
    if (-not $Registry.agents.$Agent) {
        return $Detected
    }
    $known = $Registry.agents.$Agent.capabilities | ForEach-Object { $_.id }
    return $Detected | Where-Object { $_.id -notin $known }
}

function Update-Registry {
    param($Registry, [string]$Agent, $NewCapabilities, [string]$Pr)
    if (-not $Registry.agents.$Agent) {
        $Registry.agents | Add-Member -Name $Agent -MemberType NoteProperty -Value @{
            capabilities = @()
        } -Force
    }
    foreach ($cap in $NewCapabilities) {
        $entry = [PSCustomObject]@{
            id = $cap.id
            category = $cap.category
            detected_at = (Get-Date -Format "yyyy-MM-dd")
            source_pr = "#$Pr"
            evidence = $cap.evidence
            confidence = "medium"
        }
        $Registry.agents.$Agent.capabilities += $entry
    }
    $Registry.last_updated = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
    return $Registry
}

function Save-Registry {
    param($Registry)
    $path = Join-Path $RepoRoot "agent-capabilities.json"
    $Registry | ConvertTo-Json -Depth 5 | Set-Content $path -Encoding UTF8
    Write-Output "Registry updated at $path"
}

function New-CapabilityPR {
    param([string]$Agent, $Capabilities, [string]$SourcePr)
    $capList = ($Capabilities | ForEach-Object { "- **$($_.id)** ($($_.category)): $($_.description)" }) -join "`n"
    $body = @"
## New capabilities detected for $Agent

Detected from PR $SourcePr:

$capList

### Evidence
$($Capabilities | ForEach-Object { "- $($_.id): $($_.evidence)" } | Out-String)

---

*Auto-detected by capability-monitor.ps1 — please review before merging.*
"@

    $branch = "feat/capability-update-$($Agent.Replace('@',''))-$(Get-Date -Format 'yyyyMMddHHmmss')"

    git checkout -b $branch 2>$null
    git add agent-capabilities.json
    git commit -m "feat(agents): $Agent new capabilities detected from PR $SourcePr`n`nDetected: $($Capabilities.id -join ', ')"
    git push origin $branch 2>$null

    gh pr create --title "feat(agents): $Agent capability update" `
                 --body $body `
                 --base main `
                 --head $branch

    git checkout main 2>$null
}

# ========== MAIN ==========

try {
    $diff = Get-PrDiff -Pr $PrNumber
    $rules = Load-JsonFile (Join-Path $PSScriptRoot "capability-rules.json")
    $registry = Load-JsonFile (Join-Path $RepoRoot "agent-capabilities.json")

    $detected = Detect-Capabilities -Diff $diff -Rules $rules
    $newCapabilities = Compare-WithRegistry -Detected $detected -Registry $registry -Agent $AgentName

    if ($newCapabilities.Count -eq 0) {
        Write-Output "No new capabilities detected for $AgentName in PR #$PrNumber"
        exit 0
    }

    Write-Output "Detected $($newCapabilities.Count) new capabilities for $AgentName:"
    $newCapabilities | ForEach-Object { Write-Output "  - $($_.id) ($($_.category))" }

    $registry = Update-Registry -Registry $registry -Agent $AgentName -NewCapabilities $newCapabilities -Pr $PrNumber
    Save-Registry -Registry $registry

    if ($TriggerMode -eq "auto") {
        New-CapabilityPR -Agent $AgentName -Capabilities $newCapabilities -SourcePr "#$PrNumber"
    }

    Write-Output "Capability monitor completed successfully."
} catch {
    Write-Error "Capability monitor failed: $_"
    exit 1
}
