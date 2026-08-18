[CmdletBinding()]
param(
    [string] $Version = '',
    [string] $Output = 'artifacts/packages',
    [switch] $Sign,
    [switch] $IncludeLocalMessaging
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet.exe' }
$projects = @(
    'src/Shared/Core/His.Hope.Core/His.Hope.Core.csproj',
    'src/Shared/Contracts/His.Hope.Contracts/His.Hope.Contracts.csproj',
    'src/Shared/AspNetCore/His.Hope.AspNetCore/His.Hope.AspNetCore.csproj',
    'src/Shared/Validation/His.Hope.Validation/His.Hope.Validation.csproj',
    'src/Shared/ServiceDefaults/His.Hope.ServiceDefaults/His.Hope.ServiceDefaults.csproj',
    'src/Shared/Messaging/His.Hope.Messaging.Abstractions/His.Hope.Messaging.Abstractions.csproj',
    'src/Shared/Observability/His.Hope.Observability/His.Hope.Observability.csproj',
    'src/Shared/Authorization/His.Hope.Authorization/His.Hope.Authorization.csproj',
    'src/Shared/Resilience/His.Hope.Resilience/His.Hope.Resilience.csproj',
    'src/Shared/Observability/His.Hope.Observability.OpenTelemetry/His.Hope.Observability.OpenTelemetry.csproj',
    'src/Shared/Persistence/His.Hope.Persistence/His.Hope.Persistence.csproj',
    'src/Shared/Messaging/His.Hope.Messaging.RabbitMq/His.Hope.Messaging.RabbitMq.csproj',
    'src/Shared/Messaging/His.Hope.Messaging.Redis/His.Hope.Messaging.Redis.csproj',
    'src/Shared/Messaging/His.Hope.Messaging.Sql/His.Hope.Messaging.Sql.csproj'
)
if ($IncludeLocalMessaging) {
    $projects += 'src/Shared/Messaging/His.Hope.Messaging/His.Hope.Messaging.csproj'
}

$outputPath = Join-Path $repoRoot $Output
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$common = @('pack', '-c', 'Release', '--no-restore', '-o', $outputPath)
if ($Version) { $common += @('-p:PackageVersion=' + $Version) }

foreach ($project in $projects) {
    $arguments = $common + @(Join-Path $repoRoot $project)
    $process = Start-Process -FilePath $dotnet -ArgumentList $arguments -Wait -NoNewWindow -PassThru
    if ($process.ExitCode -ne 0) { throw "Failed to pack $project." }
}

if ($Sign) {
    $certificate = $env:NUGET_SIGN_CERTIFICATE
    $password = $env:NUGET_SIGN_PASSWORD
    if ([string]::IsNullOrWhiteSpace($certificate) -or [string]::IsNullOrWhiteSpace($password)) {
        throw 'Signing requires NUGET_SIGN_CERTIFICATE and NUGET_SIGN_PASSWORD.'
    }

    Get-ChildItem -LiteralPath $outputPath -Filter '*.nupkg' | ForEach-Object {
        $process = Start-Process -FilePath $dotnet -ArgumentList @('nuget', 'sign', $_.FullName, '--certificate-path', $certificate, '--certificate-password', $password, '--timestamper', 'http://timestamp.digicert.com') -Wait -NoNewWindow -PassThru
        if ($process.ExitCode -ne 0) { throw "Failed to sign $($_.Name)." }
    }
}

Write-Host "Shared platform packages written to $outputPath"
