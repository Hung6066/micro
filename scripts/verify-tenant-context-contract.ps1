[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

$failures = [System.Collections.Generic.List[string]]::new()
$controlPlane = @(
    'IdentityService.Api',
    'ExternalIntegrationService.Api',
    'DatabaseContinuityService.Api'
)

$apiProjects = Get-ChildItem (Join-Path $Root 'src/Services') -Filter '*.Api.csproj' -Recurse
foreach ($project in $apiProjects) {
    $name = $project.BaseName
    if ($controlPlane -contains $name) { continue }

    $program = Join-Path $project.DirectoryName 'Program.cs'
    if (-not (Test-Path -LiteralPath $program)) {
        $failures.Add("${name}: missing Program.cs")
        continue
    }

    # Host composition may live in a service-specific extension rather than
    # Program.cs. Inspect the project's production C# sources so the gate
    # validates runtime registration instead of enforcing one file layout.
    $source = (Get-ChildItem -LiteralPath $project.DirectoryName -Recurse -Filter '*.cs' -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
    if ($source -notmatch '\bUseHisHopeTenantScope\s*\(') {
        $failures.Add("${name}: missing UseHisHopeTenantScope()")
    }
}

$contextOnlyContracts = @(
    @{ Name = 'ManufacturingService'; Path = 'src/Services/ManufacturingService/ManufacturingService.Api/ManufacturingApiExtensions.cs' },
    @{ Name = 'CommerceService'; Path = 'src/Services/CommerceService/CommerceService.Api/Program.cs' },
    @{ Name = 'ContentService'; Path = 'src/Services/ContentService/ContentService.Api/Program.cs' }
)
foreach ($contract in $contextOnlyContracts) {
    $path = Join-Path $Root $contract.Path
    if (-not (Test-Path -LiteralPath $path) -or (Get-Content -LiteralPath $path -Raw) -notmatch '\.RequireTenantContext\s*\(') {
        $failures.Add("$($contract.Name): missing RequireTenantContext() endpoint boundary")
    }
}

# New frontend code must use the shared tenant interceptor/header. Tests and
# documentation may mention the legacy query string as a regression fixture.
$frontendRoots = @('admin-app', 'dashboard-app', 'internal-operator-app', 'manufacturing-buyer-app', 'shared/frontend-foundation') |
    ForEach-Object { Join-Path $Root $_ }
foreach ($frontendRoot in $frontendRoots) {
    if (-not (Test-Path -LiteralPath $frontendRoot)) { continue }
    $legacy = Get-ChildItem $frontendRoot -Recurse -File -Include *.ts,*.html,*.tsx,*.jsx |
        Where-Object { $_.FullName -notmatch '(node_modules|dist|\.spec\.|\.test\.)' } |
        Select-String -Pattern 'tenantKey=' -SimpleMatch
    foreach ($match in $legacy) {
        $failures.Add("frontend legacy tenant query: $($match.Path):$($match.LineNumber)")
    }
}

$buyerInterceptor = Join-Path $Root 'manufacturing-buyer-app/src/app/core/interceptors/tenant-context.interceptor.ts'
if (-not (Test-Path -LiteralPath $buyerInterceptor)) {
    $failures.Add('manufacturing-buyer-app: missing canonical tenant context interceptor')
}

$telemetryRegistration = Join-Path $Root 'src/Shared/Observability/His.Hope.Observability.OpenTelemetry/OpenTelemetryRegistration.cs'
if (-not (Test-Path -LiteralPath $telemetryRegistration) -or
    (Get-Content -LiteralPath $telemetryRegistration -Raw) -notmatch 'AddMeter\("His\.Hope\.AspNetCore\.Tenancy"\)') {
    $failures.Add('tenant telemetry meter is not registered with OpenTelemetry')
}

Write-Output "Checked $($apiProjects.Count) service API projects and $($frontendRoots.Count) frontend roots."
if ($failures.Count -gt 0) {
    Write-Error ("Tenant context contract failed:`n - " + ($failures -join "`n - "))
    exit 1
}

Write-Output 'Tenant context contract passed.'
