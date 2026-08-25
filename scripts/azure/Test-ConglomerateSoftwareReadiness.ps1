[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Push-Location $repoRoot
try {
    & "$repoRoot/scripts/azure/Seed-ConglomerateTenant.ps1" -ValidateOnly | Out-Null

    $requiredFiles = @(
        'config/conglomerate/iam-scopes.v1.json',
        'config/conglomerate/oidc-clients.azure-staging.json',
        'src/Services/IdentityService/IdentityService.Api/appsettings.Azure.Staging.json',
        'src/Services/IdentityService/IdentityService.Application/Conglomerate/ConglomerateTenantRegistry.cs',
        'src/Shared/Authorization/His.Hope.Authorization/TenantAccessEvaluator.cs'
    )
    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file))) {
            throw "Missing required software artifact: $file"
        }
    }

    $appsettings = Get-Content -LiteralPath (Join-Path $repoRoot 'src/Services/IdentityService/IdentityService.Api/appsettings.Azure.Staging.json') -Raw | ConvertFrom-Json
    if ($appsettings.Conglomerate.Enabled -ne $true) {
        throw 'appsettings.Azure.Staging.json must set Conglomerate.Enabled=true.'
    }
    if (-not $appsettings.Conglomerate.OidcClientTenants.'manufacturing-app') {
        throw 'OidcClientTenants.manufacturing-app is required.'
    }

    if (-not $SkipTests) {
        dotnet test (Join-Path $repoRoot 'tests/Services/IdentityService/IdentityService.Application.Tests/IdentityService.Application.Tests.csproj') `
            --configuration Release `
            --filter "FullyQualifiedName~OpenIddictPopulateTokenClaims" `
            --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Identity Application tenant claim tests failed.' }

        dotnet test (Join-Path $repoRoot 'tests/Shared/Authorization.Tests/Authorization.Tests.csproj') `
            --configuration Release `
            --filter "FullyQualifiedName~cross_tenant" `
            --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Authorization cross-tenant tests failed.' }
    }

    $report = [ordered]@{
        checkedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
        phase        = 'software-first'
        passed       = $true
        message      = 'Conglomerate software readiness gate passed. Proceed to Azure infra only after local SSO pilot.'
    }
    $outDir = Join-Path $repoRoot 'artifacts/conglomerate'
    if (-not (Test-Path -LiteralPath $outDir)) {
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    }
    $reportPath = Join-Path $outDir 'software-readiness.json'
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding utf8
    $report | ConvertTo-Json -Depth 5
}
finally {
    Pop-Location
}
