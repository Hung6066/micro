$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceRoots = @(
    (Join-Path $repoRoot 'src/Services'),
    (Join-Path $repoRoot 'src/Bff'),
    (Join-Path $repoRoot 'src/ApiGateway')
)

$files = Get-ChildItem $sourceRoots -Recurse -Filter '*.cs' | Where-Object {
    $_.FullName -notmatch '\\(bin|obj|Tests?)\\' -and
    $_.FullName -notmatch '\.Tests?\\'
}

function Assert-NoMatchesOutside {
    param(
        [string]$Pattern,
        [string[]]$AllowedSuffixes,
        [string]$Boundary
    )

    $matches = foreach ($file in $files) {
        if (Select-String -LiteralPath $file.FullName -Pattern $Pattern -Quiet) {
            $file
        }
    }

    $unexpected = $matches | Where-Object {
        $relative = $_.FullName.Substring($repoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        -not ($AllowedSuffixes | Where-Object {
                $allowed = $_.Replace('\', '/').TrimEnd('/')
                $relative -like "*$allowed" -or $relative.StartsWith("$allowed/", [StringComparison]::OrdinalIgnoreCase)
            })
    }

    if ($unexpected) {
        $paths = $unexpected | ForEach-Object { $_.FullName.Substring($repoRoot.Length).TrimStart('\', '/') }
        throw "$Boundary has non-standard access: $($paths -join ', ')"
    }
}

Assert-NoMatchesOutside -Pattern 'AddGrpcClient\s*<' -AllowedSuffixes @(
    'src/Shared/Configuration/His.Hope.Configuration/GrpcClientRegistrationExtensions.cs'
) -Boundary 'gRPC client registration'

Assert-NoMatchesOutside -Pattern 'AddRabbitMQEventBus\s*\(' -AllowedSuffixes @(
    'src/Shared/EventBus/Src/His.Hope.EventBusRabbitMQ/Implementations/EventBusServiceExtensions.cs',
    'src/Shared/Infrastructure/His.Hope.Infrastructure/Messaging/RabbitMqCompatibilityExtensions.cs'
) -Boundary 'RabbitMQ registration'

Assert-NoMatchesOutside -Pattern 'new\s+HttpClient\s*(\(|\{)' -AllowedSuffixes @(
    'src/ApiGateway/Program.cs',
    'src/Services/RemediationOperator/Program.cs'
) -Boundary 'raw HttpClient construction'

# Redis multiplexer injection is intentionally limited to primitives that need
# atomic commands, streams, or security/session state. Business cache access is
# required to use ICacheService and is therefore not on this allowlist.
Assert-NoMatchesOutside -Pattern 'IConnectionMultiplexer' -AllowedSuffixes @(
    'src/ApiGateway/Program.cs',
    'src/Bff/His.Hope.Bff.Core/DependencyInjection.cs',
    'src/Bff/His.Hope.Bff.Core/Authentication/SessionAuthMiddleware.cs',
    'src/Bff/His.Hope.Bff.Core/Authentication/OidcSetup.cs',
    'src/Bff/His.Hope.Bff.Core/Authentication/CsrfValidatorMiddleware.cs',
    'src/Bff/SystemDashboard.Bff/Program.cs',
    'src/Services/DatabaseContinuityService/DatabaseContinuityService.Api/Program.cs',
    'src/Services/DatabaseContinuityService/DatabaseContinuityService.Api/ContinuityJobStore.cs',
    'src/Services/CommerceService/CommerceService.Api/Middleware/CommerceSecurityMiddleware.cs',
    'src/Services/IdentityService/IdentityService.Infrastructure/Services/',
    'src/Services/IdentityService/IdentityService.Api/Middleware/SecurityVersionMiddleware.cs',
    'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceSupportTypes.cs',
    'src/Services/IdentityService/IdentityService.Api/Services/',
    'src/Services/IdentityService/IdentityService.Api/Jobs/RedisAdminJobStore.cs',
    'src/Services/IdentityService/IdentityService.Api/Security/BffSessionGuard.cs',
    'src/Services/IdentityService/IdentityService.Api/Endpoints/PasskeyEndpoints.cs',
    'src/Services/IdentityService/IdentityService.Api/Endpoints/MobilePlatformEndpoints.cs',
    'src/Services/IdentityService/IdentityService.Api/Endpoints/MfaEndpoints.cs',
    'src/Services/IdentityService/IdentityService.Api/Endpoints/HrWebhookEndpoints.cs',
    'src/Services/IdentityService/IdentityService.Api/Endpoints/AccountRecoveryEndpoints.cs',
    'src/Services/IdentityService/IdentityService.Api/Endpoints/AdminIncidentEndpoints.cs',
    'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceEndpointExtensions.cs',
    'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServicePipelineExtensions.cs',
    'src/Services/IdentityService/IdentityService.Api/Composition/IdentityServiceRegistrationExtensions.cs'
) -Boundary 'raw Redis access'

Write-Host 'Communication boundary standardization passed: outbound clients, RabbitMQ, Redis and raw HttpClient accesses are controlled.'
