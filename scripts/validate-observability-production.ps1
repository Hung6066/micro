[CmdletBinding()]
param(
    [string]$Namespace = "monitoring",
    [switch]$Runtime
)

$ErrorActionPreference = "Stop"

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    Write-Host "PASS: $Message"
}

Assert-Condition (Test-Path "k8s/observability/production-secrets.yaml") "Vault observability SecretProviderClass exists"
Assert-Condition (Test-Path "k8s/observability/production-linkerd-authorization.yaml") "production Linkerd mTLS authorization exists"
$secretText = Get-Content "k8s/observability/production-secrets.yaml" -Raw
Assert-Condition ($secretText -notmatch "admin|xxxxx|example.com|SLACK_WEBHOOK_URL: https") "no placeholder receiver credential is committed"
Assert-Condition ($secretText -match "secret/data/his-hope/observability/grafana-oidc") "Grafana OIDC Vault path is declared"
Assert-Condition ($secretText -match "secret/data/his-hope/observability/alertmanager") "Alertmanager Vault path is declared"
Assert-Condition ($secretText -match "secret/data/his-hope/observability/object-store") "object-store Vault path is declared"

if ($Runtime) {
    $deployments = kubectl -n $Namespace get deployment -o json | ConvertFrom-Json
    foreach ($name in @("prometheus", "alertmanager", "grafana", "loki", "jaeger")) {
        $item = @($deployments.items | Where-Object { $_.metadata.name -eq $name }) | Select-Object -First 1
        Assert-Condition ($null -ne $item) "runtime deployment $name exists"
        Assert-Condition ($item.spec.replicas -ge 2) "runtime deployment $name has at least two replicas"
    }
}

Write-Host "Observability production contract validation completed."
