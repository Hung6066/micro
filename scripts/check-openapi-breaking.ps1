param(
    [Parameter(Mandatory = $true)] [string] $Baseline,
    [Parameter(Mandatory = $true)] [string] $Current
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Baseline)) { throw "OpenAPI baseline not found: $Baseline" }
if (-not (Test-Path -LiteralPath $Current)) { throw "OpenAPI current document not found: $Current" }

# Keep the breaking-change policy in one script so local and CI checks use the
# same tool and flags. A missing baseline/current document is a hard failure.
npx --yes openapi-diff "$Baseline" "$Current" --fail-on-incompatible
if ($LASTEXITCODE -ne 0) { throw "OpenAPI breaking-change check failed." }
