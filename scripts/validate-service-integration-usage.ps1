$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scanRoots = @(
    (Join-Path $root 'src/Services'),
    (Join-Path $root 'src/Bff'),
    (Join-Path $root 'src/ApiGateway'),
    (Join-Path $root 'src/Shared/ServiceDefaults')
)

$files = Get-ChildItem -LiteralPath $scanRoots -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '[\\/]bin[\\/]|[\\/]obj[\\/]|[\\/]Tests?[\\/]' }

function Get-Matches([string]$text, [string]$pattern) {
    return [regex]::Matches($text, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
}

$contents = @{}
foreach ($file in $files) {
    $contents[$file.FullName] = Get-Content -LiteralPath $file.FullName -Raw
}

$failures = [System.Collections.Generic.List[string]]::new()

$grpcRegistrations = 0
$grpcCallSites = 0
$httpRegistrations = 0
$httpCallSites = 0
$rabbitRegistrations = 0
$rabbitUsageFiles = 0
$redisUsageFiles = 0
$databaseUsageFiles = 0

foreach ($file in $files) {
    $text = $contents[$file.FullName]
    $grpc = Get-Matches $text 'AddHisHopeGrpcClient<(?<client>[A-Za-z0-9_\.]+)>'
    foreach ($registration in $grpc) {
        $grpcRegistrations++
        $clientType = $registration.Groups['client'].Value.Split('.')[-1]
        $usage = ($contents.GetEnumerator() | Where-Object {
            $_.Key -ne $file.FullName -and $_.Value -match [regex]::Escape($clientType)
        }).Count
        if ($usage -eq 0) {
            $failures.Add("gRPC registration has no call site: $($file.FullName) -> $clientType")
        } else {
            $grpcCallSites += $usage
        }
    }

    $http = Get-Matches $text '(?:AddHttpClient|AddHisHopeExternalHttpClient)\("(?<name>[^"]+)"'
    foreach ($registration in $http) {
        $httpRegistrations++
        $name = $registration.Groups['name'].Value
        $clientPattern = 'CreateClient\(\s*["'']' + [regex]::Escape($name) + '["'']\s*\)'
        $usage = ($contents.GetEnumerator() | Where-Object {
            $_.Value -match $clientPattern
        }).Count
        if ($usage -eq 0) {
            $failures.Add("named HTTP client has no call site: $($file.FullName) -> $name")
        } else {
            $httpCallSites += $usage
        }
    }

    if ($text -match 'AddHisHope(?:Legacy)?RabbitMqEventBus\(') {
        $rabbitRegistrations++
    }

    if ($text -match 'ICacheService|IConnectionMultiplexer|IDistributedCache') {
        if ($text -match 'GetAsync|SetAsync|RemoveAsync|StringGetAsync|StringSetAsync|Stream') {
            $redisUsageFiles++
        }
    }

    if ($text -match 'DbContext|IDbContextFactory') {
        if ($text -match 'ToListAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|SaveChangesAsync|ExecuteUpdate|ExecuteDelete|AddDbContext') {
            $databaseUsageFiles++
        }
    }
}

$rabbitUsageFiles = ($contents.GetEnumerator() | Where-Object {
    $_.Value -match 'PublishAsync|IEventBus|IIntegrationEventHandler|Consumer|Subscribe'
}).Count

if ($grpcRegistrations -eq 0) { $failures.Add('No shared gRPC registrations were found.') }
if ($httpRegistrations -eq 0) { $failures.Add('No named HTTP registrations were found.') }
if ($rabbitRegistrations -eq 0) { $failures.Add('No RabbitMQ registrations were found.') }
if ($rabbitUsageFiles -eq 0) { $failures.Add('RabbitMQ is registered but no publish/consume usage was found.') }
if ($redisUsageFiles -eq 0) { $failures.Add('No Redis read/write usage was found.') }
if ($databaseUsageFiles -eq 0) { $failures.Add('No database query/write usage was found.') }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Output ('Service integration usage passed: gRPC registrations={0}, gRPC call-site references={1}, named HTTP registrations={2}, HTTP call sites={3}, RabbitMQ registrations={4}, RabbitMQ usage files={5}, Redis usage files={6}, database usage files={7}.' -f `
    $grpcRegistrations, $grpcCallSites, $httpRegistrations, $httpCallSites, $rabbitRegistrations, $rabbitUsageFiles, $redisUsageFiles, $databaseUsageFiles)
