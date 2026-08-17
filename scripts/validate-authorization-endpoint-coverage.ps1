[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Get-Location).Path,
    [string]$OutputPath,
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$servicesRoot = Join-Path $RepositoryRoot 'src/Services'
$rows = [System.Collections.Generic.List[object]]::new()
$unprotected = [System.Collections.Generic.List[object]]::new()

Get-ChildItem -LiteralPath $servicesRoot -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -match '\.Api[\\/]Endpoints[\\/]' } |
    ForEach-Object {
        $path = $_.FullName
        $relative = $path.Substring($RepositoryRoot.Length).TrimStart('\')
        $lines = Get-Content -LiteralPath $path
        $protectedGroups = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $anonymousGroups = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $routeGroupParameters = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $parameterProtected = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match 'this\s+RouteGroupBuilder\s+(\w+)') { [void]$routeGroupParameters.Add($Matches[1]) }
            if ($lines[$i] -match 'var\s+(\w+)\s*=') {
                $groupName = $Matches[1]
                $groupEnd = [Math]::Min($lines.Count - 1, $i + 4)
                $groupText = ($lines[$i..$groupEnd] -join "`n")
                if ($groupText -match '\.RequireAuthorization\(') { [void]$protectedGroups.Add($groupName) }
                if ($groupText -match '\.AllowAnonymous\(') { [void]$anonymousGroups.Add($groupName) }
            }
        }
        foreach ($parameter in $routeGroupParameters) {
            if (($lines -join "`n") -match ('\b' + [regex]::Escape($parameter) + '\.RequireAuthorization\(')) {
                [void]$parameterProtected.Add($parameter)
            }
        }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -notmatch '\b(\w+)\.Map(Post|Put|Patch|Delete)\("([^"]*)"') { continue }
            $receiver = $Matches[1]
            $verb = $Matches[2].ToUpperInvariant()
            $route = $Matches[3]
            $end = [Math]::Min($lines.Count - 1, $i + 120)
            for ($j = $i + 1; $j -le $end; $j++) {
                if ($lines[$j] -match '^\s*\w+\.Map(Post|Put|Patch|Delete|Get)\(') {
                    $end = $j - 1
                    break
                }
            }
            $snippet = ($lines[$i..$end] -join "`n")
            $explicit = $snippet -match '\.RequireAuthorization\('
            $anonymous = ($snippet -match '\.AllowAnonymous\(') -or $anonymousGroups.Contains($receiver)
            $groupProtected = $protectedGroups.Contains($receiver)
            $indirect = $routeGroupParameters.Contains($receiver)
            $customAuthenticator = $relative -match 'HrWebhookEndpoints\.cs' -and (($lines -join "`n") -match 'SignatureHeader|ComputeSignature')
            $protected = $explicit -or $groupProtected -or $parameterProtected.Contains($receiver)
            $classification = if ($anonymous) { 'anonymous' } elseif ($protected) { 'protected' } elseif ($customAuthenticator) { 'custom-auth' } elseif ($indirect) { 'indirect-route-group' } else { 'missing' }
            $row = [pscustomobject]@{
                file = $relative
                line = $i + 1
                method = $verb
                route = $route
                receiver = $receiver
                classification = $classification
                evidence = if ($explicit) { 'endpoint.RequireAuthorization' } elseif ($groupProtected -or $parameterProtected.Contains($receiver)) { 'routeGroup.RequireAuthorization' } elseif ($customAuthenticator) { 'custom HMAC webhook signature' } elseif ($indirect) { 'RouteGroupBuilder parameter; caller must be audited' } elseif ($anonymous) { 'AllowAnonymous' } else { 'none' }
            }
            $rows.Add($row)
            if ($classification -eq 'missing') { $unprotected.Add($row) }
        }
    }

if ($OutputPath) {
    $resolved = Join-Path $RepositoryRoot $OutputPath
    $rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resolved -Encoding utf8
}

Write-Output ("AUTHORIZATION_ENDPOINT_INVENTORY total={0} protected={1} anonymous={2} missing={3}" -f `
    $rows.Count,
    @($rows | Where-Object classification -eq 'protected').Count,
    @($rows | Where-Object classification -eq 'anonymous').Count,
    $unprotected.Count)

if ($unprotected.Count -gt 0) {
    $unprotected | ForEach-Object { Write-Output ("AUTHORIZATION_ENDPOINT_MISSING {0}:{1} {2} {3}" -f $_.file, $_.line, $_.method, $_.route) }
    if ($Strict) { throw "Authorization endpoint coverage failed: $($unprotected.Count) mutating endpoints are not protected." }
    Write-Output 'AUTHORIZATION_ENDPOINT_COVERAGE_REVIEW_REQUIRED'
    exit 0
}

Write-Output 'AUTHORIZATION_ENDPOINT_COVERAGE_PASS'
