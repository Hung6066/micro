$ErrorActionPreference = 'Stop'
$env:KUBECONFIG = 'D:\AI\micro\artifacts\kubeconfig-production.yaml'
$token = (Get-Content -Raw 'D:\secure\his-hope\vault-k3s-production-init-output.json' | ConvertFrom-Json).root_token
$connections = [ordered]@{
    'identity-service-db' = 'identitydb'
    'appointment-service-db' = 'appointmentdb'
    'billing-service-db' = 'billingdb'
    'clinical-service-db' = 'clinicaldb'
    'lab-service-db' = 'labdb'
    'patient-service-db' = 'patientdb'
    'pharmacy-service-db' = 'pharmacydb'
}
$pw = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String((kubectl -n his-hope get secret postgres-secret -o json | ConvertFrom-Json).data.password))
$pwB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($pw))
foreach ($entry in $connections.GetEnumerator()) {
    $role = $entry.Key
    $database = $entry.Value
    $schema = if ($database -eq 'billingdb') { 'billing' } else { 'public' }
    $sql = @"
CREATE ROLE "{{name}}" WITH LOGIN PASSWORD '{{password}}' VALID UNTIL '{{expiration}}';
GRANT CONNECT ON DATABASE $database TO "{{name}}";
GRANT USAGE, CREATE ON SCHEMA $schema TO "{{name}}";
GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES ON ALL TABLES IN SCHEMA $schema TO "{{name}}";
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA $schema TO "{{name}}";
ALTER DEFAULT PRIVILEGES IN SCHEMA $schema GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES ON TABLES TO "{{name}}";
ALTER DEFAULT PRIVILEGES IN SCHEMA $schema GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO "{{name}}";
"@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($sql))
    $config = "$role-connection"
    # The plugin defaults to plaintext inside the cluster; omit a query string so
    # the Vault CLI cannot split `?sslmode=disable` into a second argument.
    $connectionUrl = "postgresql://{{username}}:{{password}}@his-hope-postgres.his-hope.svc.cluster.local:5432/$database"
    $configCommand = 'echo ' + $pwB64 + ' | base64 -d > /tmp/pgpw; export PGPW=$(cat /tmp/pgpw); VAULT_ADDR=https://127.0.0.1:8200 VAULT_SKIP_VERIFY=true VAULT_TOKEN=' + $token + ' vault write database/config/' + $config + ' plugin_name=postgresql-database-plugin allowed_roles=' + $role + " connection_url='$connectionUrl' username=his_hope password=`$PGPW"
    kubectl -n his-hope exec vault-1 -- sh -c $configCommand | Out-Null
      $revocation = 'DROP OWNED BY "{{name}}" CASCADE; DROP ROLE IF EXISTS "{{name}}";'
      $command = "echo $encoded | base64 -d | VAULT_ADDR=https://127.0.0.1:8200 VAULT_SKIP_VERIFY=true VAULT_TOKEN='$token' vault write database/roles/$role db_name=$config creation_statements=- revocation_statements=`"$revocation`""
    kubectl -n his-hope exec vault-1 -- sh -c $command | Out-Null
}
Write-Output 'Vault database connections and roles updated per service database with schema CREATE grants.'
