$ErrorActionPreference = 'Stop'
$namespace = 'his-hope'
$targets = @(
    @('api-gateway', 'api-gateway', 'ConnectionStrings__Redis'),
    @('appointment-service', 'appointment-service', 'Redis__ConnectionString'),
    @('billing-service', 'billing-service', 'Redis__ConnectionString'),
    @('clinical-service', 'clinical-service', 'Redis__ConnectionString'),
    @('lab-service', 'lab-service', 'Redis__ConnectionString'),
    @('patient-service', 'patient-service', 'Redis__ConnectionString'),
    @('pharmacy-service', 'pharmacy-service', 'Redis__ConnectionString'),
    @('billing-bff', 'billing-bff', 'ConnectionStrings__Redis'),
    @('clinical-bff', 'clinical-bff', 'ConnectionStrings__Redis'),
    @('dashboard-bff', 'dashboard-bff', 'ConnectionStrings__Redis'),
    @('lab-bff', 'lab-bff', 'ConnectionStrings__Redis'),
    @('patient-bff', 'patient-bff', 'ConnectionStrings__Redis'),
    @('pharmacy-bff', 'pharmacy-bff', 'ConnectionStrings__Redis'),
    @('systemdashboard-bff', 'systemdashboard-bff', 'ConnectionStrings__Redis')
)

$redisSecret = (kubectl -n $namespace get secret redis-secret -o json | ConvertFrom-Json).data.password
$redisPassword = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($redisSecret))
$encodedRedisPassword = [Uri]::EscapeDataString($redisPassword)
$redisUrl = "rediss://:$encodedRedisPassword@his-hope-redis:6379"
$secretPatch = @{ stringData = @{ 'redis-url' = $redisUrl } } | ConvertTo-Json -Compress
kubectl -n $namespace patch secret systemdashboard-bff-secrets --type=merge -p $secretPatch | Out-Null

foreach ($target in $targets) {
    $deployment = kubectl -n $namespace get deployment "his-hope-$($target[0])" -o json | ConvertFrom-Json
    $container = $deployment.spec.template.spec.containers | Where-Object name -eq $target[1]
    if (-not $container) { continue }
    if (-not $container.env) { $container.env = @() }
    foreach ($item in @(
        @{ name = $target[2]; valueFrom = @{ secretKeyRef = @{ name = 'systemdashboard-bff-secrets'; key = 'redis-url' } } },
        @{ name = 'Redis__TlsCaFile'; value = '/etc/tls/redis/ca.crt' }
    )) {
        $existing = @($container.env) | Where-Object name -eq $item.name
        if ($existing) { $container.env = @($container.env | Where-Object name -ne $item.name) }
        $container.env += [pscustomobject]$item
    }
    if (-not $container.volumeMounts) { $container.volumeMounts = @() }
    if (-not (@($container.volumeMounts) | Where-Object name -eq 'redis-tls-ca')) {
        $container.volumeMounts += [pscustomobject]@{ name = 'redis-tls-ca'; mountPath = '/etc/tls/redis'; readOnly = $true }
    }
    if (-not $deployment.spec.template.spec.volumes) { $deployment.spec.template.spec.volumes = @() }
    if (-not (@($deployment.spec.template.spec.volumes) | Where-Object name -eq 'redis-tls-ca')) {
        $deployment.spec.template.spec.volumes += [pscustomobject]@{ name = 'redis-tls-ca'; secret = [pscustomobject]@{ secretName = 'redis-tls'; defaultMode = 440; items = @([pscustomobject]@{ key = 'ca.crt'; path = 'ca.crt' }) } }
    }
    $json = $deployment | ConvertTo-Json -Depth 100 -Compress
    $json | kubectl apply -f - | Out-Null
}

$identityPatch = @{ spec = @{ template = @{ spec = @{ containers = @(@{ name = 'identity-service'; env = @(@{ name = 'Redis__TlsCaFile'; value = '/etc/tls/redis/ca.crt' }); volumeMounts = @(@{ name = 'redis-tls-ca'; mountPath = '/etc/tls/redis'; readOnly = $true }) }); volumes = @(@{ name = 'redis-tls-ca'; secret = @{ secretName = 'redis-tls'; defaultMode = 440; items = @(@{ key = 'ca.crt'; path = 'ca.crt' }) } }) } } } } | ConvertTo-Json -Depth 20 -Compress
kubectl -n $namespace patch deployment his-hope-identity-service --type=strategic -p $identityPatch | Out-Null
Write-Output 'Redis TLS secret, CA mounts, and connection settings applied.'
