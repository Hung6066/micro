param(
    [string] $Url = 'http://localhost:5001/swagger/v1/swagger.json',
    [string] $Output = 'artifacts/openapi/identity-v1.json',
    [int] $TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$outputDirectory = Split-Path -Parent $Output
if ($outputDirectory) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
do {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            $document = $response.Content | ConvertFrom-Json
            if ($document.openapi -notmatch '^3\.') {
                throw "Generated document is not OpenAPI 3: $Url"
            }
            if ($null -eq $document.paths) {
                throw "Generated OpenAPI document has no paths: $Url"
            }
            [IO.File]::WriteAllText($Output, ($document | ConvertTo-Json -Depth 100))
            Write-Host "Generated OpenAPI document: $Output"
            exit 0
        }
    }
    catch {
        $lastError = $_.Exception.Message
    }
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $deadline)

throw "Timed out waiting for OpenAPI endpoint '$Url'. Last error: $lastError"
