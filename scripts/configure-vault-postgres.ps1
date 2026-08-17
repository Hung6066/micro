$ErrorActionPreference = 'Stop'
$env:KUBECONFIG = 'D:\AI\micro\artifacts\kubeconfig-production.yaml'
$token = (Get-Content -Raw 'D:\secure\his-hope\vault-k3s-production-init-output.json' | ConvertFrom-Json).root_token
$pw = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String((kubectl -n his-hope get secret postgres-secret -o json | ConvertFrom-Json).data.password))
$envString = "VAULT_ADDR=https://127.0.0.1:8200 VAULT_SKIP_VERIFY=true VAULT_TOKEN='$token' POSTGRES_PASSWORD='$pw'"
$args = 'vault write database/config/identity-postgres plugin_name=postgresql-database-plugin allowed_roles=identity-service-db,appointment-service-db,billing-service-db,clinical-service-db,lab-service-db,patient-service-db,pharmacy-service-db connection_url="postgresql://{{username}}:{{password}}@his-hope-postgres.his-hope.svc.cluster.local:5432/postgres?sslmode=disable" username=his_hope password="$POSTGRES_PASSWORD"'
kubectl -n his-hope exec vault-1 -- sh -c "export $envString; $args" | Out-Null
Write-Output 'Vault PostgreSQL connection password configured.'
