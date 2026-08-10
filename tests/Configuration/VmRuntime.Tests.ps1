BeforeAll {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $RenderScript = Join-Path $RepoRoot 'deploy\vm\render-runtime-env.ps1'
    $VmValidator = Join-Path $RepoRoot 'scripts\config\validate-vm-runtime.ps1'
    $WindowsValidator = Join-Path $RepoRoot 'deploy\vm\windows\Validate-HisHopeService.ps1'

    function Invoke-PowerShellFile {
        param(
            [Parameter(Mandatory)][string]$FilePath,
            [string[]]$Arguments = @()
        )

        $output = & pwsh -NoProfile -File $FilePath @Arguments 2>&1
        [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output   = ($output | ForEach-Object { "$_" }) -join [Environment]::NewLine
        }
    }

    function Write-EnvFile {
        param(
            [Parameter(Mandatory)][string]$Path,
            [Parameter(Mandatory)][hashtable]$Values
        )

        $lines = foreach ($entry in ($Values.GetEnumerator() | Sort-Object Name)) {
            '{0}={1}' -f $entry.Key, $entry.Value
        }

        Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
    }

    function New-BaseVmValues {
        param(
            [Parameter(Mandatory)][ValidateSet('staging', 'production')][string]$Environment
        )

        $values = [ordered]@{
            HIS_HOPE_ENVIRONMENT                        = $Environment
            HIS_HOPE_RUNTIME_CONTRACT_VERSION           = 'v1'
            HIS_HOPE_PUBLIC_API_ORIGIN                  = 'https://api.staging.his-hope.example'
            HIS_HOPE_PUBLIC_WEB_ORIGIN                  = 'https://app.staging.his-hope.example'
            HIS_HOPE_PUBLIC_ADMIN_ORIGIN                = 'https://admin.staging.his-hope.example'
            HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN            = 'https://ops.staging.his-hope.example'
            HIS_HOPE_OIDC_AUTHORITY                     = 'https://identity.staging.his-hope.example'
            HIS_HOPE_SECRET_PROVIDER                    = 'vault'
            HIS_HOPE_SECRET_PROVIDER_REF                = 'kv/data/his-hope/runtime'
            SERVICE_API_GATEWAY_URL                     = 'http://gateway.internal.staging.his-hope.example:5000'
            SERVICE_IDENTITY_URL                        = 'http://identity.internal.staging.his-hope.example:5001'
            SERVICE_PATIENT_URL                         = 'http://patient.internal.staging.his-hope.example:5002'
            SERVICE_APPOINTMENT_URL                     = 'http://appointment.internal.staging.his-hope.example:5003'
            SERVICE_CLINICAL_URL                        = 'http://clinical.internal.staging.his-hope.example:5004'
            SERVICE_LAB_URL                             = 'http://lab.internal.staging.his-hope.example:5010'
            SERVICE_BILLING_URL                         = 'http://billing.internal.staging.his-hope.example:5020'
            SERVICE_PHARMACY_URL                        = 'http://pharmacy.internal.staging.his-hope.example:5030'
            SERVICE_DASHBOARD_BFF_URL                   = 'http://dashboard.internal.staging.his-hope.example:5600'
            SERVICE_DATABASE_CONTINUITY_URL             = 'http://database-continuity.internal.staging.his-hope.example:5800'
            DATABASE_POSTGRES_URL                       = 'postgresql://his_hope_app@postgres.internal.staging.his-hope.example:5432/hishope'
            REDIS_URL                                   = 'redis://redis.internal.staging.his-hope.example:6379'
            RABBITMQ_URL                                = 'amqp://his_hope_app@rabbitmq.internal.staging.his-hope.example:5672'
            RESILIENCE_HTTP_TIMEOUT_SECONDS             = '30'
            RESILIENCE_HTTP_RETRY_COUNT                 = '3'
            OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT   = 'http://otel.internal.staging.his-hope.example:4317'
            OBSERVABILITY_PROMETHEUS_URL                = 'http://prometheus.internal.staging.his-hope.example:9090'
            OBSERVABILITY_PROMETHEUS_REQUIRED           = 'false'
            OBSERVABILITY_ELASTICSEARCH_URL             = 'http://elasticsearch.internal.staging.his-hope.example:9200'
            OBSERVABILITY_ELASTICSEARCH_REQUIRED        = 'false'
            OBSERVABILITY_JAEGER_URL                    = 'http://jaeger.internal.staging.his-hope.example:16686'
            OBSERVABILITY_JAEGER_REQUIRED               = 'false'
            SECRET_POSTGRES_PASSWORD                    = '__FROM_SECRET_PROVIDER__'
            SECRET_POSTGRES_PASSWORD_REF                = 'kv/data/his-hope/postgres#password'
            SECRET_RABBITMQ_PASSWORD                    = '__FROM_SECRET_PROVIDER__'
            SECRET_RABBITMQ_PASSWORD_REF                = 'kv/data/his-hope/rabbitmq#password'
            SECRET_REDIS_PASSWORD                       = '__FROM_SECRET_PROVIDER__'
            SECRET_REDIS_PASSWORD_REF                   = 'kv/data/his-hope/redis#password'
            SECRET_OIDC_CLIENT_SECRET                   = '__FROM_SECRET_PROVIDER__'
            SECRET_OIDC_CLIENT_SECRET_REF               = 'kv/data/his-hope/oidc#client-secret'
        }

        if ($Environment -eq 'production') {
            $values.HIS_HOPE_PUBLIC_API_ORIGIN = 'https://api.his-hope.example'
            $values.HIS_HOPE_PUBLIC_WEB_ORIGIN = 'https://app.his-hope.example'
            $values.HIS_HOPE_PUBLIC_ADMIN_ORIGIN = 'https://admin.his-hope.example'
            $values.HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN = 'https://ops.his-hope.example'
            $values.HIS_HOPE_OIDC_AUTHORITY = 'https://identity.his-hope.example'
        }

        return $values
    }

    function Protect-DirectoryAcl {
        param([Parameter(Mandatory)][string]$Path)

        if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT) {
            return
        }

        $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $icaclsOutput = & icacls $Path /inheritance:r /grant:r "BUILTIN\Administrators:(OI)(CI)(F)" "NT AUTHORITY\SYSTEM:(OI)(CI)(F)" "${currentUser}:(OI)(CI)(F)" 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "icacls failed to protect test directory [$Path]: $($icaclsOutput -join ' ')"
        }
    }
}

Describe 'render-runtime-env.ps1' {
    It 'renders VM endpoints and excludes secret value keys' {
        $envFile = Join-Path $TestDrive 'vm.env'
        $outputDirectory = Join-Path $TestDrive 'rendered'
        Write-EnvFile -Path $envFile -Values (New-BaseVmValues -Environment staging)

        $result = Invoke-PowerShellFile -FilePath $RenderScript -Arguments @(
            '-ServiceName', 'identityservice',
            '-EnvironmentFile', $envFile,
            '-OutputDirectory', $outputDirectory
        )

        $renderedFile = Join-Path $outputDirectory 'identityservice.env'
        $renderedContent = Get-Content -LiteralPath $renderedFile -Raw

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'VM_RUNTIME_ENV_RENDERED'
        $renderedContent | Should -Match 'SERVICE_IDENTITY_URL=http://identity.internal.staging.his-hope.example:5001'
        $renderedContent | Should -Match 'HIS_HOPE_VM_HEALTHCHECK_URL=http://identity.internal.staging.his-hope.example:5001/health/ready'
        $renderedContent | Should -Match 'SECRET_POSTGRES_PASSWORD_FILE=/etc/his-hope/secrets/identityservice/postgres-password'
        $renderedContent | Should -Not -Match '(?m)^SECRET_POSTGRES_PASSWORD='
    }

    It 'rejects unknown service names' {
        $envFile = Join-Path $TestDrive 'vm.env'
        $outputDirectory = Join-Path $TestDrive 'rendered'
        Write-EnvFile -Path $envFile -Values (New-BaseVmValues -Environment staging)

        $result = Invoke-PowerShellFile -FilePath $RenderScript -Arguments @(
            '-ServiceName', 'unknown-service',
            '-EnvironmentFile', $envFile,
            '-OutputDirectory', $outputDirectory
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'Unknown VM service name'
    }
}

Describe 'validate-vm-runtime.ps1' {
    It 'fails when production VM inputs use localhost' {
        $values = New-BaseVmValues -Environment production
        $values.SERVICE_IDENTITY_URL = 'http://localhost:5001'
        $envFile = Join-Path $TestDrive 'production-localhost.env'
        Write-EnvFile -Path $envFile -Values $values

        $result = Invoke-PowerShellFile -FilePath $VmValidator -Arguments @(
            '-EnvironmentFile', $envFile
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'localhost'
    }

    It 'passes VM dry-run validation for a valid staging environment' {
        $envFile = Join-Path $TestDrive 'staging.env'
        Write-EnvFile -Path $envFile -Values (New-BaseVmValues -Environment staging)

        $result = Invoke-PowerShellFile -FilePath $VmValidator -Arguments @(
            '-EnvironmentFile', $envFile
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'PASS contract'
        $result.Output | Should -Match 'ENVIRONMENT_BLOCKED systemdLiveValidation'
    }
}

Describe 'Validate-HisHopeService.ps1' {
    It 'enforces file permission policy and service-name consistency' {
        $envFile = Join-Path $TestDrive 'vm.env'
        $outputDirectory = Join-Path $TestDrive 'rendered'
        $secretDirectory = Join-Path $TestDrive 'secrets'
        Write-EnvFile -Path $envFile -Values (New-BaseVmValues -Environment staging)
        $null = New-Item -ItemType Directory -Path $secretDirectory -Force
        Protect-DirectoryAcl -Path $secretDirectory

        $null = Invoke-PowerShellFile -FilePath $RenderScript -Arguments @(
            '-ServiceName', 'patientservice',
            '-EnvironmentFile', $envFile,
            '-OutputDirectory', $outputDirectory
        )

        $result = Invoke-PowerShellFile -FilePath $WindowsValidator -Arguments @(
            '-ServiceName', 'patientservice',
            '-EnvironmentDirectory', $outputDirectory,
            '-SecretDirectory', $secretDirectory,
            '-SkipServiceLookup'
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'PASS fileAcl'
        $result.Output | Should -Match 'PASS secretAcl'
        $result.Output | Should -Match 'PASS serviceName'
    }
}
