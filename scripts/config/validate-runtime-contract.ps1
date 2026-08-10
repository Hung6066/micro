[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$EnvironmentFile,

    [Parameter(Mandatory)]
    [ValidateSet('docker', 'vm', 'kubernetes')]
    [string]$Runtime,

    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-HashtableKey {
    param(
        [hashtable]$Table,
        [string]$Key
    )

    return $null -ne $Table -and $Table.ContainsKey($Key)
}

function Add-ValidationError {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Message
    )

    $Errors.Add($Message)
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

        $key = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim()
        $values[$key] = $value
    }

    return $values
}

function ConvertTo-UriRecord {
    param(
        [string]$Key,
        [string]$Value
    )

    try {
        $uri = [System.Uri]$Value
    }
    catch {
        throw "[$Key] must be an absolute URI."
    }

    if (-not $uri.IsAbsoluteUri) {
        throw "[$Key] must be an absolute URI."
    }

    if ([string]::IsNullOrWhiteSpace($uri.Host)) {
        throw "[$Key] must include a host."
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

    if ($port -lt 1 -or $port -gt 65535) {
        throw "[$Key] must include a valid port."
    }

    [pscustomobject]@{
        Key    = $Key
        Uri    = $uri
        Scheme = $uri.Scheme
        Host   = $uri.Host
        Port   = $port
        Value  = $Value
    }
}

function Assert-BooleanValue {
    param(
        [string]$Key,
        [string]$Value
    )

    if ($Value -notin @('true', 'false')) {
        throw "[$Key] must be true or false."
    }
}

function Assert-IntegerValue {
    param(
        [string]$Key,
        [string]$Value,
        [int]$Minimum
    )

    $parsed = 0
    if (-not [int]::TryParse($Value, [ref]$parsed)) {
        throw "[$Key] must be an integer."
    }

    if ($parsed -lt $Minimum) {
        throw "[$Key] must be greater than or equal to $Minimum."
    }
}

function ConvertTo-Hashtable {
    param(
        [Parameter(Mandatory)]
        $InputObject
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        $table = @{}
        foreach ($key in $InputObject.Keys) {
            $table[[string]$key] = ConvertTo-Hashtable -InputObject $InputObject[$key]
        }

        return $table
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and $InputObject -isnot [string]) {
        $items = New-Object System.Collections.ArrayList
        foreach ($item in $InputObject) {
            [void]$items.Add((ConvertTo-Hashtable -InputObject $item))
        }

        return ,$items.ToArray()
    }

    if ($InputObject -is [psobject] -and @($InputObject.PSObject.Properties).Count -gt 0) {
        $table = @{}
        foreach ($property in $InputObject.PSObject.Properties) {
            $table[$property.Name] = ConvertTo-Hashtable -InputObject $property.Value
        }

        return $table
    }

    return $InputObject
}

function Assert-StringEnum {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Path,
        [string]$Value,
        [string[]]$AllowedValues
    )

    if ($Value -notin $AllowedValues) {
        Add-ValidationError -Errors $Errors -Message "$Path must be one of: $($AllowedValues -join ', ')."
    }
}

function Assert-RuntimeTarget {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Path,
        $Target
    )

    if (-not ($Target -is [hashtable])) {
        Add-ValidationError -Errors $Errors -Message "$Path must be an object."
        return
    }

    foreach ($requiredKey in @('host', 'port')) {
        if (-not (Test-HashtableKey -Table $Target -Key $requiredKey)) {
            Add-ValidationError -Errors $Errors -Message "$Path.$requiredKey is required."
        }
    }

    if (Test-HashtableKey -Table $Target -Key 'host') {
        $hostValue = [string]$Target['host']
        if ([string]::IsNullOrWhiteSpace($hostValue)) {
            Add-ValidationError -Errors $Errors -Message "$Path.host must not be empty."
        }
    }

    if (Test-HashtableKey -Table $Target -Key 'port') {
        $portValue = 0
        if (-not [int]::TryParse([string]$Target['port'], [ref]$portValue) -or $portValue -lt 1 -or $portValue -gt 65535) {
            Add-ValidationError -Errors $Errors -Message "$Path.port must be an integer between 1 and 65535."
        }
    }
}

function Assert-EndpointDefinition {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Path,
        $Endpoint,
        [bool]$RequireTypeField,
        [string[]]$AllowedKinds
    )

    if (-not ($Endpoint -is [hashtable])) {
        Add-ValidationError -Errors $Errors -Message "$Path must be an object."
        return
    }

    $requiredKeys = @('key', 'kind', 'logicalName', 'uriSchemes', 'runtimes')
    if ($RequireTypeField) {
        $requiredKeys = @('key', 'type', 'kind', 'logicalName', 'uriSchemes', 'runtimes')
    }

    foreach ($requiredKey in $requiredKeys) {
        if (-not (Test-HashtableKey -Table $Endpoint -Key $requiredKey)) {
            Add-ValidationError -Errors $Errors -Message "$Path.$requiredKey is required."
        }
    }

    if (Test-HashtableKey -Table $Endpoint -Key 'key') {
        if ([string]$Endpoint['key'] -notmatch '^[A-Z0-9_]+$') {
            Add-ValidationError -Errors $Errors -Message "$Path.key must match ^[A-Z0-9_]+$."
        }
    }

    if ($RequireTypeField -and (Test-HashtableKey -Table $Endpoint -Key 'type')) {
        Assert-StringEnum -Errors $Errors -Path "$Path.type" -Value ([string]$Endpoint['type']) -AllowedValues @('endpoint')
    }

    if (Test-HashtableKey -Table $Endpoint -Key 'kind') {
        Assert-StringEnum -Errors $Errors -Path "$Path.kind" -Value ([string]$Endpoint['kind']) -AllowedValues $AllowedKinds
    }

    if (Test-HashtableKey -Table $Endpoint -Key 'logicalName') {
        if ([string]$Endpoint['logicalName'] -notmatch '^[a-z0-9.-]+$') {
            Add-ValidationError -Errors $Errors -Message "$Path.logicalName must match ^[a-z0-9.-]+$."
        }
    }

    if (Test-HashtableKey -Table $Endpoint -Key 'uriSchemes') {
        $uriSchemes = @($Endpoint['uriSchemes'])
        if ($uriSchemes.Count -lt 1) {
            Add-ValidationError -Errors $Errors -Message "$Path.uriSchemes must contain at least one item."
        }
        else {
            foreach ($scheme in $uriSchemes) {
                Assert-StringEnum -Errors $Errors -Path "$Path.uriSchemes" -Value ([string]$scheme) -AllowedValues @('http', 'https', 'postgresql', 'redis', 'amqp')
            }
        }
    }

    if (Test-HashtableKey -Table $Endpoint -Key 'runtimes') {
        $runtimes = $Endpoint['runtimes']
        if (-not ($runtimes -is [hashtable])) {
            Add-ValidationError -Errors $Errors -Message "$Path.runtimes must be an object."
        }
        else {
            foreach ($runtimeKey in @('docker', 'vm', 'kubernetes')) {
                if (-not (Test-HashtableKey -Table $runtimes -Key $runtimeKey)) {
                    Add-ValidationError -Errors $Errors -Message "$Path.runtimes.$runtimeKey is required."
                    continue
                }

                Assert-RuntimeTarget -Errors $Errors -Path "$Path.runtimes.$runtimeKey" -Target $runtimes[$runtimeKey]
            }
        }
    }
}

function Assert-ScalarSettingDefinition {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        [string]$Path,
        $Setting
    )

    if (-not ($Setting -is [hashtable])) {
        Add-ValidationError -Errors $Errors -Message "$Path must be an object."
        return
    }

    foreach ($requiredKey in @('key', 'type')) {
        if (-not (Test-HashtableKey -Table $Setting -Key $requiredKey)) {
            Add-ValidationError -Errors $Errors -Message "$Path.$requiredKey is required."
        }
    }

    if ((Test-HashtableKey -Table $Setting -Key 'key') -and [string]$Setting['key'] -notmatch '^[A-Z0-9_]+$') {
        Add-ValidationError -Errors $Errors -Message "$Path.key must match ^[A-Z0-9_]+$."
    }

    if (Test-HashtableKey -Table $Setting -Key 'type') {
        Assert-StringEnum -Errors $Errors -Path "$Path.type" -Value ([string]$Setting['type']) -AllowedValues @('integer', 'boolean')
    }
}

function Assert-RuntimeContractShape {
    param(
        [System.Collections.Generic.List[string]]$Errors,
        $SchemaDefinition,
        $ContractDefinition
    )

    if (-not ($SchemaDefinition -is [hashtable])) {
        Add-ValidationError -Errors $Errors -Message 'runtime-contract.schema.json must parse to a JSON object.'
        return
    }

    if (-not ($ContractDefinition -is [hashtable])) {
        Add-ValidationError -Errors $Errors -Message 'runtime-contract.v1.json must parse to a JSON object.'
        return
    }

    foreach ($requiredKey in @('version', 'requiredKeys', 'publicOrigins', 'serviceEndpoints', 'dataEndpoints', 'resilience', 'observability', 'secretProvider', 'secretReferences')) {
        if (-not (Test-HashtableKey -Table $ContractDefinition -Key $requiredKey)) {
            Add-ValidationError -Errors $Errors -Message "runtime-contract.v1.json missing top-level key [$requiredKey]."
        }
    }

    if ((Test-HashtableKey -Table $ContractDefinition -Key 'version') -and [string]$ContractDefinition['version'] -ne 'v1') {
        Add-ValidationError -Errors $Errors -Message 'runtime-contract.v1.json version must equal v1.'
    }

    if (Test-HashtableKey -Table $ContractDefinition -Key 'requiredKeys') {
        $requiredKeys = @($ContractDefinition['requiredKeys'])
        if ($requiredKeys.Count -lt 1) {
            Add-ValidationError -Errors $Errors -Message 'runtime-contract.v1.json requiredKeys must contain at least one item.'
        }
    }

    foreach ($item in @($ContractDefinition['publicOrigins'])) {
        Assert-EndpointDefinition -Errors $Errors -Path 'publicOrigins[]' -Endpoint $item -RequireTypeField:$false -AllowedKinds @('public-origin', 'oidc-authority')
    }

    foreach ($item in @($ContractDefinition['serviceEndpoints'])) {
        Assert-EndpointDefinition -Errors $Errors -Path 'serviceEndpoints[]' -Endpoint $item -RequireTypeField:$false -AllowedKinds @('service-url')
    }

    foreach ($item in @($ContractDefinition['dataEndpoints'])) {
        Assert-EndpointDefinition -Errors $Errors -Path 'dataEndpoints[]' -Endpoint $item -RequireTypeField:$false -AllowedKinds @('data-url')
    }

    foreach ($item in @($ContractDefinition['resilience'])) {
        Assert-ScalarSettingDefinition -Errors $Errors -Path 'resilience[]' -Setting $item
    }

    foreach ($item in @($ContractDefinition['observability'])) {
        $entry = ConvertTo-Hashtable -InputObject $item
        if ((Test-HashtableKey -Table $entry -Key 'type') -and [string]$entry['type'] -eq 'endpoint') {
            Assert-EndpointDefinition -Errors $Errors -Path 'observability[]' -Endpoint $entry -RequireTypeField:$true -AllowedKinds @('observability-url')
            continue
        }

        Assert-ScalarSettingDefinition -Errors $Errors -Path 'observability[]' -Setting $entry
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$schemaPath = Join-Path $repoRoot 'config\runtime-contract.schema.json'
$contractPath = Join-Path $repoRoot 'config\runtime-contract.v1.json'

if (-not (Test-Path -LiteralPath $EnvironmentFile)) {
    Write-Output "Environment file not found."
    exit 1
}

$schemaDefinition = $null
$contractDefinition = $null

try {
    $schemaDefinition = ConvertTo-Hashtable -InputObject ((Get-Content -LiteralPath $schemaPath -Raw) | ConvertFrom-Json)
    $contractDefinition = ConvertTo-Hashtable -InputObject ((Get-Content -LiteralPath $contractPath -Raw) | ConvertFrom-Json)
}
catch {
    Write-Output 'Runtime contract JSON files must contain valid JSON.'
    exit 1
}

$contractValidationErrors = [System.Collections.Generic.List[string]]::new()
Assert-RuntimeContractShape -Errors $contractValidationErrors -SchemaDefinition $schemaDefinition -ContractDefinition $contractDefinition
if ($contractValidationErrors.Count -gt 0) {
    $contractValidationErrors | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
    exit 1
}

$contract = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
$environmentValues = Read-EnvironmentFile -Path $EnvironmentFile
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($requiredKey in $contract.requiredKeys) {
    if (-not $environmentValues.Contains($requiredKey) -or [string]::IsNullOrWhiteSpace([string]$environmentValues[$requiredKey])) {
        Add-ValidationError -Errors $errors -Message "Missing required key [$requiredKey]."
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Output $_ }
    exit 1
}

if ($environmentValues['HIS_HOPE_RUNTIME_CONTRACT_VERSION'] -ne $contract.version) {
    Add-ValidationError -Errors $errors -Message "HIS_HOPE_RUNTIME_CONTRACT_VERSION must equal $($contract.version)."
}

$environmentName = [string]$environmentValues['HIS_HOPE_ENVIRONMENT']
if ($environmentName -notin @('development', 'staging', 'production')) {
    Add-ValidationError -Errors $errors -Message 'HIS_HOPE_ENVIRONMENT must be development, staging, or production.'
}

$providerKey = [string]$environmentValues[$contract.secretProvider.providerKey]
$providerRefKey = [string]$environmentValues[$contract.secretProvider.providerRefKey]
if ($providerKey -notin $contract.secretProvider.allowedProviders) {
    Add-ValidationError -Errors $errors -Message "[$($contract.secretProvider.providerKey)] must be one of: $($contract.secretProvider.allowedProviders -join ', ')."
}

if ([string]::IsNullOrWhiteSpace($providerRefKey)) {
    Add-ValidationError -Errors $errors -Message "[$($contract.secretProvider.providerRefKey)] must not be empty."
}

$endpointContracts = @($contract.publicOrigins) + @($contract.serviceEndpoints) + @($contract.dataEndpoints) + @($contract.observability | Where-Object { $_.PSObject.Properties.Name -contains 'uriSchemes' })
$endpointRecords = [System.Collections.Generic.List[object]]::new()

foreach ($endpoint in $endpointContracts) {
    $key = [string]$endpoint.key
    $value = [string]$environmentValues[$key]

    try {
        $record = ConvertTo-UriRecord -Key $key -Value $value
        if ($record.Scheme -notin $endpoint.uriSchemes) {
            Add-ValidationError -Errors $errors -Message "[$key] must use one of these schemes: $($endpoint.uriSchemes -join ', ')."
        }

        if ($environmentName -eq 'production') {
            if ($endpoint.kind -in @('public-origin', 'oidc-authority') -and $record.Scheme -ne 'https') {
                Add-ValidationError -Errors $errors -Message "[$key] must use https in production."
            }

            if (-not $endpoint.allowLocalhostInProduction -and $record.Host -eq 'localhost') {
                Add-ValidationError -Errors $errors -Message "[$key] must not use localhost in production."
            }
        }

        if ($Strict -and $endpoint.strictRuntimeMatch) {
            $runtimeTarget = $endpoint.runtimes.$Runtime
            if ($record.Host -ne $runtimeTarget.host -or $record.Port -ne [int]$runtimeTarget.port) {
                Add-ValidationError -Errors $errors -Message "[$key] must match runtime [$Runtime] host [$($runtimeTarget.host)] and port [$($runtimeTarget.port)]."
            }
        }

        $endpointRecords.Add([pscustomobject]@{
            Key         = $key
            LogicalName = [string]$endpoint.logicalName
            Host        = $record.Host
            Port        = $record.Port
        })
    }
    catch {
        Add-ValidationError -Errors $errors -Message $_.Exception.Message
    }
}

$logicalEndpointGroups = $endpointRecords | Where-Object { $_.LogicalName -notlike 'public-*' -and $_.LogicalName -ne 'oidc-authority' } | Group-Object Host, Port
foreach ($group in $logicalEndpointGroups) {
    if ($group.Count -gt 1) {
        $keys = $group.Group.Key -join ', '
        Add-ValidationError -Errors $errors -Message "Found duplicate logical endpoints for [$keys] at [$($group.Group[0].Host):$($group.Group[0].Port)]."
    }
}

foreach ($setting in $contract.resilience) {
    try {
        Assert-IntegerValue -Key $setting.key -Value ([string]$environmentValues[$setting.key]) -Minimum ([int]$setting.minimum)
    }
    catch {
        Add-ValidationError -Errors $errors -Message $_.Exception.Message
    }
}

foreach ($setting in $contract.observability | Where-Object { $_.PSObject.Properties.Name -notcontains 'uriSchemes' }) {
    try {
        Assert-BooleanValue -Key $setting.key -Value ([string]$environmentValues[$setting.key])
    }
    catch {
        Add-ValidationError -Errors $errors -Message $_.Exception.Message
    }
}

foreach ($secretReference in $contract.secretReferences) {
    $secretValue = [string]$environmentValues[$secretReference.secretKey]
    $secretRefValue = [string]$environmentValues[$secretReference.secretRefKey]
    $providerPlaceholder = '__FROM_SECRET_PROVIDER__'

    if ($secretValue -eq $providerPlaceholder -and [string]::IsNullOrWhiteSpace($secretRefValue)) {
        Add-ValidationError -Errors $errors -Message "[$($secretReference.secretRefKey)] must be supplied when [$($secretReference.secretKey)] uses the provider placeholder."
    }

    if ($environmentName -eq 'production' -and [string]::IsNullOrWhiteSpace($secretRefValue)) {
        Add-ValidationError -Errors $errors -Message "[$($secretReference.secretRefKey)] must not be empty in production."
    }

    if ($secretValue -in @('postgres', 'changeme')) {
        Add-ValidationError -Errors $errors -Message "[$($secretReference.secretKey)] uses a forbidden placeholder token."
    }
}

if ($providerRefKey -in @('postgres', 'changeme')) {
    Add-ValidationError -Errors $errors -Message "[$($contract.secretProvider.providerRefKey)] uses a forbidden placeholder token."
}

if ($errors.Count -gt 0) {
    $errors | Sort-Object -Unique | ForEach-Object { Write-Output $_ }
    exit 1
}

Write-Output "RUNTIME_CONTRACT_VALID runtime=$Runtime environment=$environmentName"
exit 0
