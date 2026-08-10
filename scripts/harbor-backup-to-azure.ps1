[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HarborArchive,
    [Parameter(Mandatory)][string]$AzureEndpoint,
    [Parameter(Mandatory)][string]$AzureContainer,
    [Parameter(Mandatory)][string]$AzureSasToken,
    [string]$Prefix = 'his-hope/production/harbor',
    [string]$AzCopyPath = 'azcopy'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path -LiteralPath $HarborArchive -PathType Leaf)) { throw 'Harbor archive does not exist.' }
if ($AzureSasToken -match 'REPLACE_ME|<[^>]+>') { throw 'Placeholder SAS refused.' }
$endpointUri = [uri]$AzureEndpoint.TrimEnd('/')
if ($endpointUri.Scheme -ne 'https' -or $endpointUri.Query) { throw 'AzureEndpoint must be a base https endpoint without query.' }

$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$destination = "$($AzureEndpoint.TrimEnd('/'))/$AzureContainer/$Prefix/harbor-$stamp.tar.zst?$($AzureSasToken.TrimStart('?'))"
& $AzCopyPath copy $HarborArchive $destination --overwrite=false | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Harbor archive upload failed.' }

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $HarborArchive).Hash.ToLowerInvariant()
Write-Output "Harbor backup upload PASS: sha256=$hash"
