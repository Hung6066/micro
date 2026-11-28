BeforeAll {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    $ComposeEnvRenderer = Join-Path $RepoRoot 'docker\config\compose.runtime.env.ps1'
    $ComposeValidator = Join-Path $RepoRoot 'scripts\config\validate-compose-stack.ps1'
    $ComposeFile = Join-Path $RepoRoot 'docker\docker-compose.yml'
    $DevelopmentEnvironmentFile = Join-Path $RepoRoot 'config\environments\development.env.example'

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

    function Read-EnvironmentFile {
        param([string]$Path)

        $values = [ordered]@{}
        foreach ($line in Get-Content -LiteralPath $Path) {
            $trimmed = $line.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
                continue
            }

            $separatorIndex = $trimmed.IndexOf('=')
            if ($separatorIndex -lt 1) {
                throw "Invalid environment line format in $Path."
            }

            $values[$trimmed.Substring(0, $separatorIndex).Trim()] = $trimmed.Substring($separatorIndex + 1).Trim()
        }

        return $values
    }
}

Describe 'compose.runtime.env.ps1' {
    It 'renders a non-secret compose adapter file for development' {
        $outputFile = Join-Path $TestDrive 'compose.runtime.env'

        $result = Invoke-PowerShellFile -FilePath $ComposeEnvRenderer -Arguments @(
            '-Environment', 'development',
            '-OutputFile', $outputFile
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'COMPOSE_RUNTIME_ENV_RENDERED'

        $values = Read-EnvironmentFile -Path $outputFile
        $values['SERVICE_API_GATEWAY_URL'] | Should -Be 'http://api-gateway:5000'
        $values['SERVICE_PATIENT_GRPC_URL'] | Should -Be 'http://patientservice:5006'
        $values['DATABASE_IDENTITY_URL'] | Should -Be 'Host=postgres;Database=identitydb;Username=postgres;Password=postgres'
        $values['REDIS_URL'] | Should -Be 'redis://redis:6379'
        $values['RABBITMQ_URL'] | Should -Be 'amqp://his_hope_app@rabbitmq:5672'
        $values['SECRET_POSTGRES_PASSWORD'] | Should -Be '__FROM_SECRET_PROVIDER__'
        $values['SECRET_RABBITMQ_PASSWORD'] | Should -Be '__FROM_SECRET_PROVIDER__'
        $values['SECRET_REDIS_PASSWORD'] | Should -Be '__FROM_SECRET_PROVIDER__'
    }

    It 'renders production passkey origins from public origins without secret values' {
        $outputFile = Join-Path $TestDrive 'compose.runtime.production.env'

        $result = Invoke-PowerShellFile -FilePath $ComposeEnvRenderer -Arguments @(
            '-Environment', 'production',
            '-OutputFile', $outputFile
        )

        $result.ExitCode | Should -Be 0

        $values = Read-EnvironmentFile -Path $outputFile
        $values['PASSKEYS_RP_ID'] | Should -Be 'api.his-hope.example'
        $values['PASSKEYS_ORIGIN_0'] | Should -Be 'https://api.his-hope.example'
        $values.Keys | Should -Not -Contain 'AGENT_HARNESS_API_KEY'
    }
}

Describe 'docker compose adapter wiring' {
    It 'uses adapter variables instead of hard-coded inter-service literals for canonical dependencies' {
        $composeContent = Get-Content -LiteralPath $ComposeFile -Raw

        foreach ($expectedToken in @(
            '${SERVICE_IDENTITY_URL:-',
            '${SERVICE_PATIENT_GRPC_URL:-',
            '${SERVICE_APPOINTMENT_GRPC_URL:-',
            '${SERVICE_DASHBOARD_BFF_URL:-',
            '${DATABASE_IDENTITY_URL:-',
            '${DATABASE_PATIENT_URL:-',
            '${DATABASE_APPOINTMENT_URL:-',
            '${DATABASE_CLINICAL_URL:-',
            '${DATABASE_LAB_URL:-',
            '${DATABASE_BILLING_URL:-',
            '${DATABASE_PHARMACY_URL:-',
            '${DATABASE_HARNESS_URL:-',
            '${REDIS_URL:-',
            '${RABBITMQ_URL:-',
            '${OBSERVABILITY_OTEL_EXPORTER_OTLP_ENDPOINT:-'
        )) {
            $composeContent | Should -Match ([regex]::Escape($expectedToken))
        }

        $composeContent | Should -Not -Match ('Identity__BootstrapAdmin__Password:\s+' + ('Admin' + '@' + '123'))
        $composeContent | Should -Not -Match 'Jwt__SessionKey=ThisIsADevelopmentKeyThatIsLongEnoughForHmacSha256!'
        $composeContent | Should -Not -Match 'AgentHarness__ApiKey:\s+\$\{AGENT_HARNESS_API_KEY:-dev-key-change-in-production\}'
    }

    It 'validates docker compose config against the development runtime contract' {
        $result = Invoke-PowerShellFile -FilePath $ComposeValidator -Arguments @(
            '-ComposeFile', $ComposeFile,
            '-EnvironmentFile', $DevelopmentEnvironmentFile,
            '-Strict'
        )

        $result.ExitCode | Should -Be 0
        $result.Output | Should -Match 'RUNTIME_CONTRACT_VALID'
        $result.Output | Should -Match 'COMPOSE_CONFIG_VALID'
        $result.Output | Should -Match '"missing":\[\]'
        $result.Output | Should -Match '"mismatched":\[\]'
    }
}
