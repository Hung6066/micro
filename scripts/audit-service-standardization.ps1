[CmdletBinding()]
param(
    [string]$Root,
    [int]$MinimumOccurrences = 2,
    [int]$MinimumFiles = 2,
    [switch]$IncludeTests
)

$ErrorActionPreference = 'Stop'
$Root = if ([string]::IsNullOrWhiteSpace($Root)) {
    (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
} else {
    (Resolve-Path $Root).Path
}
$sourceRoot = Join-Path $Root 'src'
$files = Get-ChildItem $sourceRoot -Recurse -Filter '*.cs' |
    Where-Object {
        $_.FullName -notmatch '[\\/]?(bin|obj|Migrations)[\\/]' -and
        ($IncludeTests -or $_.FullName -notmatch '[\\/]Tests?[\\/]')
    }

$stringLiteralPattern = '(?<!@)"((?:\\.|[^"\\]){3,})"'
$literalOccurrences = @{}
$protocolLiteralPatterns = @(
    'Find(?:First|FirstValue|All)\("(?:sub|client_id|azp|email|name|given_name|family_name|amr|acr|tenant_membership|permissions)"\)',
    'new\s+Claim\("(?:sub|client_id|azp|email|name|given_name|family_name|amr|acr|tenant_membership|permissions)"',
    'SetClaim\("(?:sub|client_id|azp|email|name|given_name|family_name|amr|acr|tenant_membership|permissions)"'
)
$protocolLiteralOccurrences = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, $stringLiteralPattern)) {
        $value = $match.Groups[1].Value
        if ($value -match '^(?:https?://|[A-Za-z]:[\\/]|[0-9]+)$' -or
            $value -match '^[{[]' -or $value -match '[\r\n]' -or
            $value -match '^[a-z]+\.[a-z0-9_.-]+$' -or
            $value -match '^(?:character varying|timestamp with time zone|boolean|integer|text)(?:\(\d+\))?$' -or
            $value -in @('application/json', 'application/problem+json', 'global')) {
            continue
        }
        if (-not $literalOccurrences.ContainsKey($value)) {
            $literalOccurrences[$value] = [System.Collections.Generic.List[string]]::new()
        }
        $literalOccurrences[$value].Add($file.FullName.Substring($Root.Length).TrimStart([char[]]'\\/'))
    }
    if ($file.FullName -notmatch '[\\/]HisHopeProtocolConstants\.cs$') {
        foreach ($pattern in $protocolLiteralPatterns) {
            if ([regex]::IsMatch($content, $pattern)) {
                $protocolLiteralOccurrences.Add($file.FullName.Substring($Root.Length).TrimStart([char[]]'\\/'))
                break
            }
        }
    }
}

function Count-Matches([string]$Pattern) {
    $count = 0
    foreach ($file in $files) {
        $count += @(Select-String -Path $file.FullName -Pattern $Pattern -AllMatches).Count
    }
    return $count
}

Write-Output 'His.Hope service standardization audit'
Write-Output "Scope: $($files.Count) production C# files under $sourceRoot (services, BFF, gateway and shared modules)"
Write-Output ''
Write-Output 'Category                               Matches'
Write-Output '-------------------------------------  -------'
@{
    'inline Problem/HTTP result creation' = 'Results\.(Problem|BadRequest|NotFound|Conflict|Unauthorized|Forbid)'
    'direct configuration indexer' = 'Configuration\s*\['
    'connection string access' = 'GetConnectionString|ConnectionString'
    'loopback/default endpoint' = 'localhost|127\.0\.0\.1|::1'
    'secret-shaped identifier' = 'Password\s*=|ClientSecret|ApiKey|Bearer\s+[A-Za-z0-9]'
    'literal paging operation' = '(Skip|Take)\s*\(\s*\d+'
    'route mapping' = 'Map(Get|Post|Put|Patch|Delete)\s*\('
}.GetEnumerator() | Sort-Object Name | ForEach-Object {
    '{0,-37}  {1,7}' -f $_.Key, (Count-Matches $_.Value)
}

Write-Output ''
Write-Output 'Review policy:'
Write-Output '- Move cross-service protocol values to His.Hope.Contracts or His.Hope.Configuration.'
Write-Output '- Keep service-owned route and domain error codes close to that service contract.'
Write-Output '- Never move secrets into constants; bind them from Vault/environment at runtime.'
Write-Output '- Do not replace intentional security-hiding 404s or protocol-specific results blindly.'
Write-Output '- Require CancellationToken and bounded pagination for database-backed endpoints.'

Write-Output ''
Write-Output 'Protocol literal violations (must use HisHopeProtocolConstants.Claims)'
if ($protocolLiteralOccurrences.Count -eq 0) {
    Write-Output 'None'
} else {
    $protocolLiteralOccurrences | Sort-Object -Unique | ForEach-Object { Write-Output "- $_" }
}

Write-Output ''
Write-Output "Repeated hardcoded string candidates (occurrences >= $MinimumOccurrences, files >= $MinimumFiles)"
Write-Output 'Occurrences  Files  Literal'
Write-Output '-----------  -----  -------'
$literalOccurrences.GetEnumerator() |
    Where-Object { $_.Value.Count -ge $MinimumOccurrences -and ($_.Value | Select-Object -Unique).Count -ge $MinimumFiles } |
    Sort-Object { $_.Value.Count } -Descending |
    Select-Object -First 100 |
    ForEach-Object {
        $display = $_.Key -replace '\\', '\\\\' -replace "`r?`n", '\\n'
        '{0,11}  {1,5}  {2}' -f $_.Value.Count, ($_.Value | Select-Object -Unique).Count, $display
    }

Write-Output ''
Write-Output 'Ownership rules:'
Write-Output '- Wire values (claims, headers, cookies, media types) -> His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.'
Write-Output '- Cross-service DTO/event values -> His.Hope.Contracts.'
Write-Output '- Domain states/codes -> the owning service Domain module; do not leak them into SharedKernel.'
Write-Output '- UI text, secrets, URLs, SQL, test fixtures and one-off parser tokens remain local or configuration-backed.'
