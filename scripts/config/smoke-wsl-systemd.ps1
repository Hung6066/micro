[CmdletBinding()]
param(
    [string]$Distro = 'Ubuntu',
    [string]$HealthcheckUrl = 'http://identity.his-hope.local:9080/.well-known/openid-configuration',
    [string]$ResolveHost = 'identity.his-hope.local',
    [int]$ResolvePort = 9080,
    [string]$ResolveAddress = '127.0.0.1'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Wsl([string]$Command) {
    & wsl.exe -d $Distro -u root -- bash -lc $Command
    if ($LASTEXITCODE -ne 0) { throw "WSL command failed: $Command" }
}

$unitName = 'his-hope-wsl-smoke.service'
$envPath = '/etc/his-hope/his-hope-wsl-smoke.env'
$unitPath = "/etc/systemd/system/$unitName"
$curlArgs = "--fail --silent --show-error --max-time 15 --resolve $ResolveHost`:$ResolvePort`:$ResolveAddress $HealthcheckUrl"
$unit = @'
[Unit]
Description=His.Hope WSL2 systemd smoke check
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
EnvironmentFile=/etc/his-hope/his-hope-wsl-smoke.env
ExecStart=/usr/bin/curl --fail --silent --show-error --max-time 15 --resolve identity.his-hope.local:9080:127.0.0.1 http://identity.his-hope.local:9080/.well-known/openid-configuration
Restart=always
RestartSec=2
NoNewPrivileges=yes
PrivateTmp=yes

[Install]
WantedBy=multi-user.target
'@
$environment = "HIS_HOPE_VM_HEALTHCHECK_URL=$HealthcheckUrl"

try {
    Invoke-Wsl 'test "$(ps -p 1 -o comm=)" = systemd'
    Invoke-Wsl 'systemctl is-system-running >/dev/null 2>&1 || test $? -eq 0'
    Invoke-Wsl 'mkdir -p /etc/his-hope'
    $unitBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($unit))
    $envBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($environment))
    Invoke-Wsl "echo $unitBase64 | base64 -d > $unitPath; chmod 0644 $unitPath"
    Invoke-Wsl "echo $envBase64 | base64 -d > $envPath; chmod 0640 $envPath"
    Invoke-Wsl "systemctl daemon-reload; systemctl enable --now $unitName"
    Start-Sleep -Seconds 3
    Invoke-Wsl "systemctl is-active --quiet $unitName || (systemctl status --no-pager $unitName; journalctl --no-pager -u $unitName -n 20; exit 1)"
    Invoke-Wsl "systemctl restart $unitName"
    Invoke-Wsl "systemctl is-active --quiet $unitName || (systemctl status --no-pager $unitName; journalctl --no-pager -u $unitName -n 20; exit 1)"
    Write-Output "PASS wsl-systemd:distro=$Distro unit=$unitName"
}
finally {
    try {
        Invoke-Wsl ('systemctl disable --now {0} 2>/dev/null || true; rm -f {1} {2}; systemctl daemon-reload' -f $unitName, $unitPath, $envPath)
    } catch {
        Write-Warning $_
    }
}
