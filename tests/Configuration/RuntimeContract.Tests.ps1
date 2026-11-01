BeforeAll {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $ContractValidator = Join-Path $RepoRoot 'scripts\config\validate-runtime-contract.ps1'
    $ReferenceValidator = Join-Path $RepoRoot 'scripts\config\validate-runtime-references.ps1'

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

    function New-BaseContractValues {
        param(
            [Parameter(Mandatory)][ValidateSet('development', 'staging', 'production')][string]$Environment,
            [Parameter(Mandatory)][ValidateSet('docker', 'vm', 'kubernetes')][string]$Runtime
        )

        $values = [ordered]@{
            HIS_HOPE_ENVIRONMENT                        = $Environment
            HIS_HOPE_RUNTIME_CONTRACT_VERSION           = 'v1'
            HIS_HOPE_PUBLIC_API_ORIGIN                  = 'https://api.his-hope.example'
            HIS_HOPE_PUBLIC_WEB_ORIGIN                  = 'https://app.his-hope.example'
            HIS_HOPE_PUBLIC_ADMIN_ORIGIN                = 'https://admin.his-hope.example'
            HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN            = 'https://ops.his-hope.example'
            HIS_HOPE_OIDC_AUTHORITY                     = 'https://identity.his-hope.example'
            AUTHZ_PDP_MODE                              = 'disabled'
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
            SERVICE_FHIR_GATEWAY_URL                    = 'http://fhir.internal.staging.his-hope.example:5040'
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
            OBSERVABILITY_LOKI_URL                      = 'http://loki.internal.staging.his-hope.example:3100'
            OBSERVABILITY_ALERTMANAGER_URL              = 'http://alertmanager.internal.staging.his-hope.example:9093'
            SECRET_POSTGRES_PASSWORD                    = '__FROM_SECRET_PROVIDER__'
            SECRET_POSTGRES_PASSWORD_REF                = 'kv/data/his-hope/postgres#password'
            SECRET_RABBITMQ_PASSWORD                    = '__FROM_SECRET_PROVIDER__'
            SECRET_RABBITMQ_PASSWORD_REF                = 'kv/data/his-hope/rabbitmq#password'
            SECRET_REDIS_PASSWORD                       = '__FROM_SECRET_PROVIDER__'
            SECRET_REDIS_PASSWORD_REF                   = 'kv/data/his-hope/redis#password'
            SECRET_OIDC_CLIENT_SECRET                   = '__FROM_SECRET_PROVIDER__'
            SECRET_OIDC_CLIENT_SECRET_REF               = 'kv/data/his-hope/oidc#client-secret'
            DEVICE_POSTURE_MODE                         = 'observe'
            DEVICE_POSTURE_PROVIDERS                    = 'chrome-enterprise'
            DEVICE_POSTURE_TTL_SECONDS                  = '300'
            DEVICE_POSTURE_ENFORCE_CLINICAL             = 'false'
            PASSWORD_HISTORY_ENABLED                    = 'true'
            PASSWORD_HISTORY_COUNT                      = '5'
            AUDIT_APPEND_ONLY                            = 'true'
            AUDIT_REDACTION_ENABLED                     = 'true'
            EXTERNAL_FEDERATION_ENABLED                 = 'false'
            SCIM_M2M_ENABLED                            = 'false'
            PROVISIONING_MODE                           = 'dry-run'
            PROVISIONING_TARGETS                        = 'scim'
            SSF_ENABLED                                 = 'false'
            MTLS_ENABLED                                = 'false'
            RADIUS_EAP_TLS_ENABLED                      = 'false'
            CSV_EXPORT_ENABLED                          = 'false'
        }

        switch ($Runtime) {
            'docker' {
                $values.SERVICE_API_GATEWAY_URL = 'http://api-gateway:5000'
                $values.SERVICE_IDENTITY_URL = 'http://identityservice:5003'
                $values.SERVICE_PATIENT_URL = 'http://patientservice:5002'
                $values.SERVICE_APPOINTMENT_URL = 'http://appointmentservice:5003'
                $values.SERVICE_CLINICAL_URL = 'http://clinicalservice:5004'
                $values.SERVICE_LAB_URL = 'http://labservice:5010'
                $values.SERVICE_BILLING_URL = 'http://billingservice:5020'
                $values.SERVICE_PHARMACY_URL = 'http://pharmacyservice:5030'
                $values.SERVICE_FHIR_GATEWAY_URL = 'http://fhir-gateway:5040'
                $values.SERVICE_DASHBOARD_BFF_URL = 'http://dashboard-bff:5600'
                $values.SERVICE_DATABASE_CONTINUITY_URL = 'http://database-continuity-service:5800'
                $values.DATABASE_POSTGRES_URL = 'postgresql://his_hope_app@postgres:5432/hishope'
                $values.REDIS_URL = 'redis://redis:6379'
                $values.RABBITMQ_URL = 'amqp://his_hope_app@rabbitmq:5672'
                $values.OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT = 'http://otel-collector:4317'
            }
            'kubernetes' {
                $values.SERVICE_API_GATEWAY_URL = 'http://api-gateway.his-hope.svc.cluster.local:5000'
                $values.SERVICE_IDENTITY_URL = 'http://identity-service.his-hope.svc.cluster.local:5003'
                $values.SERVICE_PATIENT_URL = 'http://patient-service.his-hope.svc.cluster.local:5002'
                $values.SERVICE_APPOINTMENT_URL = 'http://appointment-service.his-hope.svc.cluster.local:5004'
                $values.SERVICE_CLINICAL_URL = 'http://clinical-service.his-hope.svc.cluster.local:5005'
                $values.SERVICE_LAB_URL = 'http://lab-service.his-hope.svc.cluster.local:5010'
                $values.SERVICE_BILLING_URL = 'http://billing-service.his-hope.svc.cluster.local:5020'
                $values.SERVICE_PHARMACY_URL = 'http://pharmacy-service.his-hope.svc.cluster.local:5030'
                $values.SERVICE_FHIR_GATEWAY_URL = 'http://fhir-gateway.his-hope.svc.cluster.local:5040'
                $values.SERVICE_DASHBOARD_BFF_URL = 'http://dashboard-bff.his-hope.svc.cluster.local:5600'
                $values.SERVICE_DATABASE_CONTINUITY_URL = 'http://database-continuity.his-hope.svc.cluster.local:5800'
                $values.DATABASE_POSTGRES_URL = 'postgresql://his_hope_app@postgres.his-hope.svc.cluster.local:5432/hishope'
                $values.REDIS_URL = 'redis://redis.his-hope.svc.cluster.local:6379'
                $values.RABBITMQ_URL = 'amqp://his_hope_app@rabbitmq.his-hope.svc.cluster.local:5672'
                $values.OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT = 'http://otel-collector.his-hope.svc.cluster.local:4317'
            }
        }

        if ($Environment -eq 'development') {
            $values.HIS_HOPE_PUBLIC_API_ORIGIN = 'http://localhost:5000'
            $values.HIS_HOPE_PUBLIC_WEB_ORIGIN = 'http://localhost:4200'
            $values.HIS_HOPE_PUBLIC_ADMIN_ORIGIN = 'http://localhost:4201'
            $values.HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN = 'http://localhost:4202'
            $values.HIS_HOPE_OIDC_AUTHORITY = 'http://localhost:5001'
        }
        elseif ($Environment -eq 'staging') {
            $values.HIS_HOPE_PUBLIC_API_ORIGIN = 'https://api.staging.his-hope.example'
            $values.HIS_HOPE_PUBLIC_WEB_ORIGIN = 'https://app.staging.his-hope.example'
            $values.HIS_HOPE_PUBLIC_ADMIN_ORIGIN = 'https://admin.staging.his-hope.example'
            $values.HIS_HOPE_PUBLIC_DASHBOARD_ORIGIN = 'https://ops.staging.his-hope.example'
            $values.HIS_HOPE_OIDC_AUTHORITY = 'https://identity.staging.his-hope.example'
        }

        return $values
    }

    function New-ComposeFixture {
        param([Parameter(Mandatory)][string]$Path)

        $compose = @'
services:
  api-gateway:
    image: scratch
    ports:
      - "5000:5000"
  identityservice:
    image: scratch
    ports:
      - "5003:5003"
  patientservice:
    image: scratch
    ports:
      - "5002:5002"
  appointmentservice:
    image: scratch
    ports:
      - "5003:5003"
  clinicalservice:
    image: scratch
    ports:
      - "5004:5004"
  labservice:
    image: scratch
    ports:
      - "5010:5010"
  billingservice:
    image: scratch
    ports:
      - "5020:5020"
  pharmacyservice:
    image: scratch
    ports:
      - "5030:5030"
  fhir-gateway:
    image: scratch
    ports:
      - "5040:5040"
  systemdashboard-bff:
    image: scratch
    ports:
      - "5700:5700"
  dashboard-bff:
    image: scratch
    ports:
      - "5600:5600"
  database-continuity-service:
    image: scratch
    ports:
      - "5800:5800"
  postgres:
    image: scratch
    ports:
      - "5433:5432"
  redis:
    image: scratch
    ports:
      - "6379:6379"
  rabbitmq:
    image: scratch
    ports:
      - "5672:5672"
  otel-collector:
    image: scratch
    ports:
      - "4317:4317"
  postgres-wal-archive-init:
    image: scratch
  postgres-replica:
    image: scratch
    ports:
      - "5432:5432"
  postgres-restore-drill:
    image: scratch
  elasticsearch:
    image: scratch
    ports:
      - "9200:9200"
  kibana:
    image: scratch
    ports:
      - "5601:5601"
  consul:
    image: scratch
    ports:
      - "8500:8500"
  jaeger:
    image: scratch
    ports:
      - "16686:16686"
      - "4317:4317"
      - "4318:4318"
  glitchtip-postgres:
    image: scratch
  glitchtip-valkey:
    image: scratch
  glitchtip:
    image: scratch
    ports:
      - "8000:8000"
  prometheus:
    image: scratch
    ports:
      - "9090:9090"
  loki:
    image: scratch
    ports:
      - "3100:3100"
  vault:
    image: scratch
    ports:
      - "8200:8200"
  vault-init:
    image: scratch
  postgres-exporter:
    image: scratch
  redis-exporter:
    image: scratch
  alertmanager:
    image: scratch
    ports:
      - "9093:9093"
  grafana:
    image: scratch
    ports:
      - "3000:3000"
  external-integration-service:
    image: scratch
    ports:
      - "5060:5060"
  frontend:
    image: scratch
    ports:
      - "8080:8080"
  admin-app:
    image: scratch
    ports:
      - "8080:8080"
  manufacturingservice:
    image: scratch
    ports:
      - "5050:5050"
  commerceservice:
    image: scratch
    ports:
      - "5015:5015"
  contentservice:
    image: scratch
    ports:
      - "5016:5016"
  manufacturing-buyer-app:
    image: scratch
    ports:
      - "8080:8080"
  internal-operator-app:
    image: scratch
    ports:
      - "8080:8080"
  dashboard-app:
    image: scratch
    ports:
      - "8080:8080"
  temporal:
    image: scratch
    ports:
      - "7233:7233"
      - "8233:8233"
  temporal-admin-tools:
    image: scratch
  temporal-worker:
    image: scratch
    ports:
      - "5270:5270"
  agentharness:
    image: scratch
    ports:
      - "5200:5200"
  certgen:
    image: scratch
'@

        Set-Content -LiteralPath $Path -Value $compose -Encoding UTF8
    }

    function New-KustomizeFixture {
        param([Parameter(Mandatory)][string]$DirectoryPath)

        $serviceDefinitions = @(
            @{ Name = 'api-gateway'; Port = 5000 },
            @{ Name = 'identity-service'; Port = 5003 },
            @{ Name = 'patient-service'; Port = 5002 },
            @{ Name = 'appointment-service'; Port = 5004 },
            @{ Name = 'clinical-service'; Port = 5005 },
            @{ Name = 'lab-service'; Port = 5010 },
            @{ Name = 'billing-service'; Port = 5020 },
            @{ Name = 'pharmacy-service'; Port = 5030 },
            @{ Name = 'dashboard-bff'; Port = 5600 },
            @{ Name = 'database-continuity'; Port = 5800 },
            @{ Name = 'postgres'; Port = 5432 },
            @{ Name = 'redis'; Port = 6379 },
            @{ Name = 'rabbitmq'; Port = 5672 },
            @{ Name = 'otel-collector'; Port = 4317 }
        )

        $resourceNames = New-Object System.Collections.Generic.List[string]
        foreach ($serviceDefinition in $serviceDefinitions) {
            $fileName = '{0}.yaml' -f $serviceDefinition.Name
            $resourceNames.Add($fileName)
            $yaml = @"
apiVersion: v1
kind: Service
metadata:
  name: $($serviceDefinition.Name)
spec:
  ports:
    - name: http
      port: $($serviceDefinition.Port)
      targetPort: $($serviceDefinition.Port)
"@
            Set-Content -LiteralPath (Join-Path $DirectoryPath $fileName) -Value $yaml -Encoding UTF8
        }

        $kustomization = @(
            'apiVersion: kustomize.config.k8s.io/v1beta1'
            'kind: Kustomization'
            'resources:'
        ) + ($resourceNames | ForEach-Object { '  - ' + $_ })

        Set-Content -LiteralPath (Join-Path $DirectoryPath 'kustomization.yaml') -Value $kustomization -Encoding UTF8
    }
}

Describe 'validate-runtime-contract.ps1' {
    It 'accepts a valid docker environment file in strict mode' {
        $envFile = Join-Path $TestDrive 'docker.env'
        Write-EnvFile -Path $envFile -Values (New-BaseContractValues -Environment development -Runtime docker)

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'docker',
            '-Strict'
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'RUNTIME_CONTRACT_VALID'
        $result.Output | Should -Not -Match '__FROM_SECRET_PROVIDER__'
    }

    It 'accepts a valid VM environment file in strict mode' {
        $envFile = Join-Path $TestDrive 'vm.env'
        Write-EnvFile -Path $envFile -Values (New-BaseContractValues -Environment staging -Runtime vm)

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'vm',
            '-Strict'
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'RUNTIME_CONTRACT_VALID'
    }

    It 'accepts a valid Kubernetes environment file in strict mode' {
        $envFile = Join-Path $TestDrive 'k8s.env'
        Write-EnvFile -Path $envFile -Values (New-BaseContractValues -Environment production -Runtime kubernetes)

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'kubernetes',
            '-Strict'
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'RUNTIME_CONTRACT_VALID'
    }

    It 'fails when a required key is missing' {
        $values = New-BaseContractValues -Environment development -Runtime docker
        $null = $values.Remove('SERVICE_LAB_URL')
        $envFile = Join-Path $TestDrive 'missing-key.env'
        Write-EnvFile -Path $envFile -Values $values

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'docker',
            '-Strict'
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'SERVICE_LAB_URL'
    }

    It 'fails when a URL is malformed' {
        $values = New-BaseContractValues -Environment development -Runtime docker
        $values.SERVICE_PATIENT_URL = 'patientservice:5002'
        $envFile = Join-Path $TestDrive 'invalid-url.env'
        Write-EnvFile -Path $envFile -Values $values

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'docker',
            '-Strict'
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'SERVICE_PATIENT_URL'
    }

    It 'fails when production internal service URLs use localhost' {
        $values = New-BaseContractValues -Environment production -Runtime kubernetes
        $values.SERVICE_IDENTITY_URL = 'http://localhost:5003'
        $envFile = Join-Path $TestDrive 'localhost-production.env'
        Write-EnvFile -Path $envFile -Values $values

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'kubernetes',
            '-Strict'
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'localhost'
    }

    It 'fails when logical service endpoints are duplicated' {
        $values = New-BaseContractValues -Environment development -Runtime docker
        $values.SERVICE_APPOINTMENT_URL = $values.SERVICE_PATIENT_URL
        $envFile = Join-Path $TestDrive 'duplicate-endpoint.env'
        Write-EnvFile -Path $envFile -Values $values

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'docker',
            '-Strict'
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'duplicate'
    }

    It 'fails when a placeholder secret has no provider reference' {
        $values = New-BaseContractValues -Environment production -Runtime kubernetes
        $values.SECRET_POSTGRES_PASSWORD_REF = ''
        $envFile = Join-Path $TestDrive 'placeholder-secret.env'
        Write-EnvFile -Path $envFile -Values $values

        $result = Invoke-PowerShellFile -FilePath $ContractValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'kubernetes',
            '-Strict'
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'SECRET_POSTGRES_PASSWORD_REF'
    }
}

Describe 'validate-runtime-references.ps1' {
    It 'accepts a docker environment whose references match compose' {
        $envFile = Join-Path $TestDrive 'docker-reference.env'
        $composeFile = Join-Path $TestDrive 'docker-compose.yml'
        Write-EnvFile -Path $envFile -Values (New-BaseContractValues -Environment development -Runtime docker)
        New-ComposeFixture -Path $composeFile

        $result = Invoke-PowerShellFile -FilePath $ReferenceValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'docker',
            '-ComposeFile', $composeFile
        )

        if ($result.ExitCode -ne 0) { throw $result.Output }
        $result.Output | Should -Match '"status":"pass"'
    }

    It 'accepts a kubernetes environment whose references match kustomize services' {
        $envFile = Join-Path $TestDrive 'k8s-reference.env'
        $kustomizeDirectory = Join-Path $TestDrive 'k8s'
        $null = New-Item -ItemType Directory -Path $kustomizeDirectory
        Write-EnvFile -Path $envFile -Values (New-BaseContractValues -Environment production -Runtime kubernetes)
        New-KustomizeFixture -DirectoryPath $kustomizeDirectory

        $result = Invoke-PowerShellFile -FilePath $ReferenceValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'kubernetes',
            '-Kustomization', (Join-Path $kustomizeDirectory 'kustomization.yaml')
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match '"status":"pass"'
    }

    It 'fails when a compose service port does not match the canonical contract' {
        $values = New-BaseContractValues -Environment development -Runtime docker
        $values.SERVICE_IDENTITY_URL = 'http://identityservice:5999'
        $envFile = Join-Path $TestDrive 'compose-mismatch.env'
        $composeFile = Join-Path $TestDrive 'compose-mismatch.yml'
        Write-EnvFile -Path $envFile -Values $values
        New-ComposeFixture -Path $composeFile

        $result = Invoke-PowerShellFile -FilePath $ReferenceValidator -Arguments @(
            '-EnvironmentFile', $envFile,
            '-Runtime', 'docker',
            '-ComposeFile', $composeFile
        )

        $result.ExitCode | Should -Not -Be 0
        $result.Output | Should -Match 'identityservice'
        $result.Output | Should -Match '5999'
    }
}
