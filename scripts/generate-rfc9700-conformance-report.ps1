[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputPath = 'artifacts/security/oidc-conformance/report.json'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
}

Push-Location $RepositoryRoot
try {
    dotnet test tests/IdentityService/IdentityService.IntegrationTests/IdentityService.IntegrationTests.csproj `
        --configuration Release `
        --filter "FullyQualifiedName~Rfc9700ConformanceTests" `
        --logger "trx;LogFileName=rfc9700-conformance.trx" `
        | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "RFC 9700 conformance tests failed." }

    $reportDirectory = Split-Path -Parent $OutputPath
    if ($reportDirectory) { New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null }

    $report = [ordered]@{
        assessmentType = 'oidc-conformance'
        evidenceSource = 'automated-repository'
        profile = 'RFC9700'
        status = 'passed'
        assessor = 'His.Hope automated RFC 9700 matrix'
        reportUri = 'https://github.com/his-hope/micro/actions'
        completedAt = [DateTimeOffset]::UtcNow.ToString('o')
        matrix = @(
            'authorization_code_requires_pkce',
            'pkce_plain_method_rejected',
            'exact_redirect_uri_enforced',
            'discovery_required_fields',
            'token_endpoint_rejects_missing_grant',
            'refresh_token_rejects_empty_token',
            'introspection_marks_invalid_token_inactive',
            'revocation_handles_empty_token_safely',
            'password_and_implicit_grants_not_advertised'
        )
    }
    $json = ($report | ConvertTo-Json -Depth 6)
    [IO.File]::WriteAllText((Join-Path $RepositoryRoot $OutputPath), $json, [Text.UTF8Encoding]::new($false))
    Write-Host "RFC 9700 conformance report written to $OutputPath"
} finally {
    Pop-Location
}
