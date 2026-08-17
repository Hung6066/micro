[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EnvironmentFile,

    [Parameter(Mandatory)]
    [ValidateSet('docker', 'vm', 'kubernetes')]
    [string]$Runtime,

    [string]$ComposeFile,

    [string]$Kustomization,

    [string]$PlatformContract
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function ConvertTo-PlainValue {
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $InputObject
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject -is [string] -or $InputObject -is [char] -or $InputObject -is [bool] -or $InputObject -is [int] -or $InputObject -is [long] -or $InputObject -is [double] -or $InputObject -is [decimal]) {
        return $InputObject
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $table = [ordered]@{}
        foreach ($key in $InputObject.Keys) {
            $table[[string]$key] = ConvertTo-PlainValue -InputObject $InputObject[$key]
        }

        return $table
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $items = New-Object System.Collections.ArrayList
        foreach ($item in $InputObject) {
            [void]$items.Add((ConvertTo-PlainValue -InputObject $item))
        }

        return ,$items.ToArray()
    }

    if ($InputObject -is [psobject] -and @($InputObject.PSObject.Properties).Count -gt 0) {
        $table = [ordered]@{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $table[$property.Name] = ConvertTo-PlainValue -InputObject $property.Value
        }

        return $table
    }

    return [string]$InputObject
}

function ConvertTo-CompactJsonValue {
    param(
        [Parameter(Mandatory)]
        [AllowNull()]
        $InputObject
    )

    if ($null -eq $InputObject) {
        return 'null'
    }

    if ($InputObject -is [string] -or $InputObject -is [char]) {
        $escaped = [string]$InputObject
        $escaped = $escaped.Replace('\', '\\')
        $escaped = $escaped.Replace('"', '\"')
        $escaped = $escaped.Replace("`r", '\r')
        $escaped = $escaped.Replace("`n", '\n')
        $escaped = $escaped.Replace("`t", '\t')
        return '"' + $escaped + '"'
    }

    if ($InputObject -is [bool]) {
        if ($InputObject) { return 'true' }
        return 'false'
    }

    if ($InputObject -is [int] -or $InputObject -is [long] -or $InputObject -is [double] -or $InputObject -is [decimal]) {
        return [string]$InputObject
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $pairs = foreach ($key in $InputObject.Keys) {
            '"' + ([string]$key).Replace('\', '\\').Replace('"', '\"') + '":' + (ConvertTo-CompactJsonValue -InputObject $InputObject[$key])
        }

        return '{' + ($pairs -join ',') + '}'
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $items = foreach ($item in $InputObject) {
            ConvertTo-CompactJsonValue -InputObject $item
        }

        return '[' + ($items -join ',') + ']'
    }

    return '"' + ([string]$InputObject).Replace('\', '\\').Replace('"', '\"') + '"'
}

function Read-EnvironmentFile {
    param([string]$Path)

    $values = [ordered]@{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) {
            throw "Invalid environment line format in $Path."
        }

        $values[$trimmed.Substring(0, $separatorIndex).Trim()] = $trimmed.Substring($separatorIndex + 1).Trim()
    }

    return $values
}

function ConvertTo-UriRecord {
    param(
        [string]$Key,
        [string]$Value
    )

    $uri = [System.Uri]$Value
    if (-not $uri.IsAbsoluteUri) {
        throw "[$Key] must be an absolute URI."
    }

    $port = if ($uri.IsDefaultPort) {
        switch ($uri.Scheme) {
            'http' { 80 }
            'https' { 443 }
            'postgresql' { 5432 }
            'redis' { 6379 }
            'amqp' { 5672 }
            default { -1 }
        }
    }
    else {
        $uri.Port
    }

    [pscustomobject]@{
        Key  = $Key
        Host = $uri.Host
        Port = $port
    }
}

function Get-ReferenceLookupKey {
    param(
        [string]$HostName,
        [string]$Runtime
    )

    if ($Runtime -eq 'kubernetes' -and $HostName -match '^[^.]+\.') {
        return $HostName.Split('.')[0]
    }

    return $HostName
}

function Get-ComposeReferences {
    param([string]$Path)

    $references = [ordered]@{}
    $lines = Get-Content -LiteralPath $Path
    $inServices = $false
    $currentService = $null
    $inPorts = $false

    foreach ($line in $lines) {
        if ($line -match '^services:\s*$') {
            $inServices = $true
            continue
        }

        if (-not $inServices) {
            continue
        }

        if ($line -match '^[A-Za-z]') {
            break
        }

        if ($line -match '^\s{2}([A-Za-z0-9._-]+):\s*$') {
            $currentService = $Matches[1]
            $references[$currentService] = @()
            $inPorts = $false
            continue
        }

        if ($line -match '^\s{4}ports:\s*$') {
            $inPorts = $true
            continue
        }

        if ($line -match '^\s{4}[A-Za-z0-9_-]+:\s*$') {
            $inPorts = $false
            continue
        }

        # Accept both PORT:CONTAINER and HOST_IP:PORT:CONTAINER forms. The
        # latter is used by local Compose to avoid Docker Desktop's ambiguous
        # dual-stack publish path while preserving the container port contract.
        if ($inPorts -and $currentService -and $line -match '^\s{6}-\s*["'']?(?:[^"''\s:]+:)?([0-9]+):([0-9]+)["'']?\s*$') {
            $references[$currentService] += [int]$Matches[2]
        }
    }

    return $references
}

function Get-KustomizeReferences {
    param([string]$Path)

    $references = [ordered]@{}

    $kustomizationDirectory = if (Test-Path -LiteralPath $Path -PathType Leaf) { Split-Path -Parent $Path } else { $Path }
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $directories = @(
        $kustomizationDirectory,
        (Join-Path $repoRoot 'k8s\base'),
        (Join-Path $repoRoot 'k8s\observability'),
        (Join-Path $repoRoot 'k8s\vault'),
        (Join-Path $repoRoot 'k8s\infrastructure')
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -Unique

    foreach ($resourceFile in ($directories | ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Include *.yaml,*.yml } | Sort-Object FullName -Unique)) {
        foreach ($document in ((Get-Content -LiteralPath $resourceFile.FullName -Raw) -split '(?m)^---\s*$')) {
            if ($document -notmatch '(?m)^\s*kind:\s*Service\s*$') { continue }
            $nameMatch = [regex]::Match($document, '(?m)^\s{2,4}name:\s*([A-Za-z0-9._-]+)\s*$')
            if (-not $nameMatch.Success) { continue }
            $ports = [regex]::Matches($document, '(?m)^\s{4,8}port:\s*([0-9]+)\s*$') |
                ForEach-Object { [int]$_.Groups[1].Value }
            if (@($ports).Count -gt 0) { $references[$nameMatch.Groups[1].Value] = @($ports) }
        }
    }

    return $references
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$contractPath = Join-Path $repoRoot 'config\runtime-contract.v1.json'
$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
$platformContractPath = if ($PlatformContract) { $PlatformContract } else { Join-Path $repoRoot 'config\platform-contract.v1.json' }
$platformDefinition = if (Test-Path -LiteralPath $platformContractPath) {
    Get-Content -LiteralPath $platformContractPath -Raw | ConvertFrom-Json
} else {
    $null
}
$environmentValues = Read-EnvironmentFile -Path $EnvironmentFile

if ($Runtime -eq 'docker' -and [string]::IsNullOrWhiteSpace($ComposeFile)) {
    Write-Output 'ComposeFile is required for docker runtime.'
    exit 1
}

if ($Runtime -eq 'kubernetes' -and [string]::IsNullOrWhiteSpace($Kustomization)) {
    Write-Output 'Kustomization is required for kubernetes runtime.'
    exit 1
}

$expectedReferences = [ordered]@{}
$referenceContracts = @($contract.serviceEndpoints) + @($contract.dataEndpoints) + @($contract.observability | Where-Object { $_.PSObject.Properties.Name -contains 'uriSchemes' -and $_.strictRuntimeMatch })
foreach ($referenceContract in $referenceContracts) {
    $uriRecord = ConvertTo-UriRecord -Key $referenceContract.key -Value ([string]$environmentValues[$referenceContract.key])
    $lookupKey = Get-ReferenceLookupKey -HostName $uriRecord.Host -Runtime $Runtime
    $expectedReferences[$lookupKey] = [pscustomobject]@{
        LookupKey = $lookupKey
        Host = $uriRecord.Host
        Port = $uriRecord.Port
        SourceKey = [string]$referenceContract.key
    }
}

$platformReferences = if ($platformDefinition) {
    switch ($Runtime) {
        'docker' { @($platformDefinition.runtimes.docker) }
        'kubernetes' { @($platformDefinition.runtimes.kubernetes) }
        'vm' { @($platformDefinition.runtimes.vm) }
    }
} else {
    @()
}
foreach ($platformReference in $platformReferences) {
        $lookupKey = [string]$platformReference.host
        $expectedReferences[$lookupKey] = [pscustomobject]@{
            LookupKey = $lookupKey
            Host = $lookupKey
            Port = if (@($platformReference.ports).Count -eq 1) { [int]@($platformReference.ports)[0] } else { $null }
            Ports = @($platformReference.ports | ForEach-Object { [int]$_ })
            Optional = if ($platformReference.PSObject.Properties.Name -contains 'optional') { [bool]$platformReference.optional } else { $false }
            SourceKey = 'platform-contract'
        }
}

$actualReferences = switch ($Runtime) {
    'docker' { Get-ComposeReferences -Path $ComposeFile }
    'kubernetes' { Get-KustomizeReferences -Path $Kustomization }
    'vm' { [ordered]@{} }
}

$consumedCompatibilityAliases = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$knownCompatibilityNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
if ($Runtime -eq 'kubernetes' -and $platformDefinition -and $platformDefinition.PSObject.Properties.Name -contains 'kubernetesCompatibilityAliases') {
    foreach ($aliasEntry in $platformDefinition.kubernetesCompatibilityAliases.PSObject.Properties) {
        $aliases = if ($aliasEntry.Value -is [System.Array]) { @($aliasEntry.Value) } else { @([string]$aliasEntry.Value) }
        foreach ($alias in $aliases) { $null = $knownCompatibilityNames.Add([string]$alias) }
        if ($actualReferences.Contains($aliasEntry.Name)) {
            foreach ($alias in $aliases) {
                if ($actualReferences.Contains([string]$alias)) {
                    $null = $consumedCompatibilityAliases.Add([string]$alias)
                }
            }
            continue
        }
        foreach ($alias in $aliases) {
            if ($actualReferences.Contains([string]$alias)) {
                $actualReferences[$aliasEntry.Name] = $actualReferences[[string]$alias]
                $null = $consumedCompatibilityAliases.Add([string]$alias)
                break
            }
        }
    }
}

$missing = [System.Collections.Generic.List[object]]::new()
$extra = [System.Collections.Generic.List[object]]::new()
$mismatched = [System.Collections.Generic.List[object]]::new()

foreach ($expected in $expectedReferences.GetEnumerator()) {
    if (-not $actualReferences.Contains($expected.Key)) {
        if ($expected.Value.Optional) { continue }
        $missing.Add([pscustomobject]@{
            host = $expected.Key
            expectedPort = $expected.Value.Port
            sourceKey = $expected.Value.SourceKey
        })
        continue
    }

    $actualPorts = @($actualReferences[$expected.Key])
    $hasPlatformPorts = $expected.Value.PSObject.Properties.Name -contains 'Ports'
    $expectedPorts = if ($hasPlatformPorts -and $expected.Value.Ports) { @($expected.Value.Ports) } elseif ($null -ne $expected.Value.Port) { @($expected.Value.Port) } else { @() }
    if (@($expectedPorts).Count -gt 0 -and -not ($expectedPorts | Where-Object { $_ -in $actualPorts })) {
        $mismatched.Add([pscustomobject]@{
            host = $expected.Key
            expectedPort = $expectedPorts
            actualPorts = $actualPorts
            sourceKey = $expected.Value.SourceKey
        })
    }
}

foreach ($actual in $actualReferences.GetEnumerator()) {
    if ($consumedCompatibilityAliases.Contains($actual.Key) -or $knownCompatibilityNames.Contains($actual.Key)) { continue }
    if (-not $expectedReferences.Contains($actual.Key)) {
        $extra.Add([pscustomobject]@{
            host = $actual.Key
            actualPorts = @($actual.Value)
        })
    }
}

$status = if ($missing.Count -eq 0 -and $extra.Count -eq 0 -and $mismatched.Count -eq 0) { 'pass' } else { 'fail' }
$result = [ordered]@{
    status = $status
    runtime = $Runtime
    missing = @($missing)
    extra = @($extra)
    mismatched = @($mismatched)
}

Write-Output "Runtime reference validation status: $status"
if ($missing.Count -gt 0) {
    Write-Output ("Missing references: " + (($missing | ForEach-Object { $_.host + ':' + $_.expectedPort }) -join ', '))
}
if ($extra.Count -gt 0) {
    Write-Output ("Extra references: " + (($extra | ForEach-Object { $_.host + ':' + ($_.actualPorts -join '/') }) -join ', '))
}
if ($mismatched.Count -gt 0) {
Write-Output ("Mismatched references: " + (($mismatched | ForEach-Object { $_.host + ' expected ' + $_.expectedPort + ' actual ' + ($_.actualPorts -join '/') }) -join ', '))
}
Write-Output (ConvertTo-CompactJsonValue -InputObject (ConvertTo-PlainValue -InputObject $result))

if ($status -eq 'pass') {
    exit 0
}

exit 1
