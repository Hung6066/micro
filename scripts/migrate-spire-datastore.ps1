[CmdletBinding()]
param(
    [string]$OldNamespace = 'his-hope-dev',
    [string]$OldPod = 'postgres-0',
    [string]$NewNamespace = 'spire',
    [string]$NewCluster = 'spire-postgres'
)

$ErrorActionPreference = 'Stop'
$dumpName = ".spiredb-migration-$([Guid]::NewGuid().ToString('N')).dump"
$sourceDumpPath = '/tmp/spiredb.dump'
$targetDumpPath = '/controller/tmp/spiredb.dump'
$oldPassword = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String((kubectl get secret spire-postgres -n spire -o jsonpath='{.data.password}')))
$newPassword = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String((kubectl get secret spire-postgres-app -n spire -o jsonpath='{.data.password}')))
$newPod = kubectl get pods -n $NewNamespace -l "cnpg.io/cluster=$NewCluster,role=primary" -o jsonpath='{.items[0].metadata.name}'
if ([string]::IsNullOrWhiteSpace($newPod)) { throw 'No CNPG primary pod found.' }

function Invoke-PsqlScalar([string]$Namespace, [string]$Pod, [string]$Password, [string]$Sql, [string]$Container = '') {
    $args = @('exec', '-n', $Namespace, $Pod)
    if ($Container) { $args += @('-c', $Container) }
    $args += @('--', 'env', "PGPASSWORD=$Password", 'psql', '--host=127.0.0.1', '--tuples-only', '--no-align', '--username=spire_server', '--dbname=spiredb', "--command=$Sql")
    return (& kubectl @args).Trim()
}

try {
    & kubectl exec -n $OldNamespace $OldPod -c postgres -- env "PGPASSWORD=$oldPassword" pg_dump --format=custom --no-owner --username=spire_server --dbname=spiredb --file=/tmp/spiredb.dump
    if ($LASTEXITCODE -ne 0) { throw 'Source pg_dump failed.' }
    & kubectl cp "$OldNamespace/${OldPod}:$sourceDumpPath" $dumpName -c postgres
    if ($LASTEXITCODE -ne 0) { throw 'Copying source dump from the cluster failed.' }
    & kubectl cp $dumpName "$NewNamespace/${newPod}:$targetDumpPath"
    if ($LASTEXITCODE -ne 0) { throw 'Copying source dump to the CNPG primary failed.' }
    & kubectl exec -n $NewNamespace $newPod -- env "PGPASSWORD=$newPassword" pg_restore --host=127.0.0.1 --clean --if-exists --no-owner --exit-on-error --username=spire_server --dbname=spiredb $targetDumpPath
    if ($LASTEXITCODE -ne 0) { throw 'Target pg_restore failed.' }

    $oldTables = Invoke-PsqlScalar $OldNamespace $OldPod $oldPassword "select count(*) from information_schema.tables where table_schema='public';" postgres
    $newTables = Invoke-PsqlScalar $NewNamespace $newPod $newPassword "select count(*) from information_schema.tables where table_schema='public';"
    $oldEntries = Invoke-PsqlScalar $OldNamespace $OldPod $oldPassword 'select count(*) from registered_entries;' postgres
    $newEntries = Invoke-PsqlScalar $NewNamespace $newPod $newPassword 'select count(*) from registered_entries;'
    if (($oldTables -ne $newTables) -or ($oldEntries -ne $newEntries)) {
        throw "Migration verification mismatch: tables $oldTables/$newTables, registered_entries $oldEntries/$newEntries."
    }
    Write-Output "SPIRE datastore migration PASS: tables=$newTables registered_entries=$newEntries"
}
finally {
    & kubectl exec -n $OldNamespace $OldPod -c postgres -- rm -f $sourceDumpPath 2>$null
    & kubectl exec -n $NewNamespace $newPod -- rm -f $targetDumpPath 2>$null
    Remove-Item -LiteralPath $dumpName -Force -ErrorAction SilentlyContinue
}
