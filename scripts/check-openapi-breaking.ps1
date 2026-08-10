param(
    [Parameter(Mandatory = $true)] [string] $Baseline,
    [Parameter(Mandatory = $true)] [string] $Current
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Baseline)) { throw "OpenAPI baseline not found: $Baseline" }
if (-not (Test-Path -LiteralPath $Current)) { throw "OpenAPI current document not found: $Current" }

# Keep the compatibility policy in-repository so local and CI checks do not
# depend on an npm registry or an unpinned transient executable. This gate
# protects the non-breaking surface: existing paths, methods, and declared
# response status codes cannot disappear.
$baselineDocument = Get-Content -Raw -LiteralPath $Baseline | ConvertFrom-Json
$currentDocument = Get-Content -Raw -LiteralPath $Current | ConvertFrom-Json

if ($baselineDocument.openapi -notmatch '^3\.' -or $currentDocument.openapi -notmatch '^3\.') {
    throw 'Both OpenAPI documents must use OpenAPI 3.'
}

$breakingChanges = [System.Collections.Generic.List[string]]::new()
foreach ($pathProperty in $baselineDocument.paths.PSObject.Properties) {
    $currentPath = $currentDocument.paths.PSObject.Properties[$pathProperty.Name]
    if ($null -eq $currentPath) {
        $breakingChanges.Add("Removed path: $($pathProperty.Name)")
        continue
    }

    foreach ($operation in $pathProperty.Value.PSObject.Properties | Where-Object { $_.Name -in @('get','post','put','patch','delete','head','options','trace') }) {
        $currentOperation = $currentPath.Value.PSObject.Properties[$operation.Name]
        if ($null -eq $currentOperation) {
            $breakingChanges.Add("Removed operation: $($operation.Name.ToUpperInvariant()) $($pathProperty.Name)")
            continue
        }

        $baselineResponses = @($operation.Value.responses.PSObject.Properties.Name)
        $currentResponses = @($currentOperation.Value.responses.PSObject.Properties.Name)
        foreach ($status in $baselineResponses) {
            if ($status -notin $currentResponses) {
                $breakingChanges.Add("Removed response $status from $($operation.Name.ToUpperInvariant()) $($pathProperty.Name)")
            }
        }
    }
}

if ($breakingChanges.Count -gt 0) {
    $breakingChanges | ForEach-Object { Write-Error $_ }
    throw "OpenAPI breaking-change check failed with $($breakingChanges.Count) incompatible change(s)."
}

Write-Host "OpenAPI compatibility check passed: existing paths, methods, and response status codes are preserved."
