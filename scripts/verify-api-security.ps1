[CmdletBinding()]
param(
    [string]$Root
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}
$failures = [System.Collections.Generic.List[string]]::new()
$apiProjects = Get-ChildItem (Join-Path $Root 'src/Services') -Filter '*.Api.csproj' -Recurse

foreach ($project in $apiProjects) {
    $program = Join-Path $project.DirectoryName 'Program.cs'
    if (-not (Test-Path $program)) {
        $failures.Add("$($project.FullName): missing Program.cs")
        continue
    }

    $source = (Get-ChildItem $project.DirectoryName -Filter '*.cs' -Recurse |
        ForEach-Object { Get-Content -Raw $_.FullName }) -join "`n"
    $name = $project.BaseName

    # Health-only/integration workers do not expose a user API. Every other
    # HTTP API must use the shared JWT and authorization bootstrap.
    if ($name -eq 'ExternalIntegrationService.Api') { continue }

    foreach ($marker in @(
        'AddHisHopeJwtAuthentication',
        'UseAuthentication',
        'UseAuthorization',
        'UseDpopAuthorizationSchemeNormalization',
        'UseDpopAccessTokenValidation'
    )) {
        if ($source -notmatch [regex]::Escape($marker)) {
            $failures.Add("${name}: missing shared security marker '$marker'")
        }
    }

    if ($source -notmatch 'AddHisHopeAuthorization') {
        $failures.Add("${name}: missing AddHisHopeAuthorization()")
    }
}

# Environment templates are repository artifacts, not secret stores. Reject
# connection strings or password assignments that contain a concrete value;
# placeholders, variable expansion and secret-manager references are allowed.
$environmentTemplate = Join-Path $Root 'docker/.env.example'
if (Test-Path -LiteralPath $environmentTemplate) {
    $templateLines = Get-Content -LiteralPath $environmentTemplate
    foreach ($line in $templateLines) {
        if ($line -match '^\s*(?:ConnectionStrings__|DATABASE_.*URL)\s*=\s*(?<value>.+?)\s*$' -and
            $Matches.value -notmatch '^\$\{|ChangeMe|YOUR_|<[^>]+>|from_vault|^$') {
            $failures.Add("${environmentTemplate}: concrete database connection value in template")
            break
        }

        if ($line -match '^\s*(?:[^#=]+)?Password\s*=\s*(?<value>.+?)\s*$' -and
            $Matches.value -notmatch '^\$\{|ChangeMe|YOUR_|<[^>]+>|^$') {
            $failures.Add("${environmentTemplate}: concrete password value in template")
            break
        }
    }
}

Write-Output "Checked $($apiProjects.Count) service API projects."
if ($failures.Count -gt 0) {
    Write-Error ("API security contract failed:`n - " + ($failures -join "`n - "))
    exit 1
}

Write-Output 'API security contract passed.'
