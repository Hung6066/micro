BeforeDiscovery {
    $ScriptPath = Join-Path $PSScriptRoot "..\capability-monitor.ps1"
    $RulesPath = Join-Path $PSScriptRoot "..\capability-rules.json"
    $RepoRoot = (git rev-parse --show-toplevel)
}

BeforeAll {
    $ScriptPath = Join-Path $PSScriptRoot "..\capability-monitor.ps1"
    $RulesPath = Join-Path $PSScriptRoot "..\capability-rules.json"
    . $ScriptPath
    $rules = Load-JsonFile $RulesPath
}

Describe "Detect-Capabilities" {

    It "Detects Redis caching pattern" {
        $diff = @"
+ using StackExchange.Redis;
+ services.AddStackExchangeRedisCache(config =>
+ {
+     config.Configuration = "localhost:6379";
+ });
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "redis-caching"
    }

    It "Does not match Redis when only 1 pattern found (min_matches=2)" {
        $diff = @"
+ services.AddDistributedMemoryCache();
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Not -Contain "redis-caching"
    }

    It "Detects Circuit Breaker pattern" {
        $diff = @"
+ var policy = Policy.Handle<Exception>()
+     .CircuitBreaker(3, TimeSpan.FromSeconds(30));
+ services.AddPolicyHandler(policy);
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "circuit-breaker"
    }

    It "Detects multiple capabilities in one diff" {
        $diff = @"
+ using StackExchange.Redis;
+ services.AddStackExchangeRedisCache(...);
+ using Polly;
+ var cb = Policy.Handle<HttpRequestException>()
+     .CircuitBreaker(3, TimeSpan.FromSeconds(30));
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "redis-caching"
        $result.id | Should -Contain "circuit-breaker"
    }

    It "Filters out patterns in code comments" {
        $diff = @"
+ // TODO: consider using StackExchange.Redis for caching
+ // AddStackExchangeRedisCache might help
+ var x = 1;
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Not -Contain "redis-caching"
    }

    It "Detects NgRx SignalStore pattern" {
        $diff = @"
+ import { signalStore, withMethods, patchState } from '@ngrx/signals';
+
+ export const PatientStore = signalStore(
+   withMethods((store) => ({
+     loadPatients() { ... }
+   }))
+ );
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "ngrx-signals"
    }

    It "Detects Material Dialog pattern" {
        $diff = @"
+ import { MatDialog, MAT_DIALOG_DATA } from '@angular/material/dialog';
+
+ constructor(private dialog: MatDialog) {}
+
+ this.dialog.open(PatientDialogComponent, {
+   data: { patientId: id }
+ });
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "material-dialog"
    }

    It "Detects gRPC client pattern" {
        $diff = @"
+ using Grpc.Net.Client;
+ using var channel = GrpcChannel.ForAddress("https://localhost:5001");
+ services.AddGrpcClient<PatientService.PatientServiceClient>();
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "grpc-client"
    }

    It "Detects Outbox pattern" {
        $diff = @"
+ var outboxMessage = new OutboxMessage(
+     typeof(PatientCreatedEvent).AssemblyQualifiedName,
+     JsonSerializer.Serialize(@event));
+ await outboxDispatcher.ProcessOutboxMessages();
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.id | Should -Contain "outbox-pattern"
    }

    It "Returns empty for no capability matches" {
        $diff = @"
+ var x = 1;
+ var y = 2;
"@
        $result = Detect-Capabilities -Diff $diff -Rules $rules
        $result.Count | Should -Be 0
    }
}

Describe "Compare-WithRegistry" {

    It "Finds new capabilities not in registry" {
        $detected = @(
            [PSCustomObject]@{ id = "redis-caching"; category = "infrastructure" },
            [PSCustomObject]@{ id = "circuit-breaker"; category = "resilience" }
        )
        $registry = @{
            version = "1"
            last_updated = ""
            agents = @{
                "@dotnet" = @{
                    capabilities = @(
                        @{ id = "redis-caching"; category = "infrastructure" }
                    )
                }
            }
        } | ConvertTo-Json -Depth 10 | ConvertFrom-Json

        $result = Compare-WithRegistry -Detected $detected -Registry $registry -Agent "@dotnet"
        $result.Count | Should -Be 1
        $result[0].id | Should -Be "circuit-breaker"
    }

    It "Returns empty when all capabilities are known" {
        $detected = @(
            [PSCustomObject]@{ id = "redis-caching"; category = "infrastructure" }
        )
        $registry = @{
            version = "1"
            last_updated = ""
            agents = @{
                "@dotnet" = @{
                    capabilities = @(
                        @{ id = "redis-caching"; category = "infrastructure" }
                    )
                }
            }
        } | ConvertTo-Json -Depth 10 | ConvertFrom-Json

        $result = Compare-WithRegistry -Detected $detected -Registry $registry -Agent "@dotnet"
        $result.Count | Should -Be 0
    }

    It "Returns all capabilities for new agent not in registry" {
        $detected = @(
            [PSCustomObject]@{ id = "redis-caching"; category = "infrastructure" }
        )
        $registry = @{
            version = "1"
            last_updated = ""
            agents = @{}
        } | ConvertTo-Json | ConvertFrom-Json

        $result = Compare-WithRegistry -Detected $detected -Registry $registry -Agent "@dotnet"
        $result.Count | Should -Be 1
        $result[0].id | Should -Be "redis-caching"
    }
}

Describe "Load-JsonFile" {

    It "Parses valid JSON file" {
        $result = Load-JsonFile $RulesPath
        $result.rules.Count | Should -BeGreaterThan 0
    }

    It "Throws on non-existent file" {
        { Load-JsonFile "nonexistent.json" } | Should -Throw
    }
}

Describe "Update-Registry" {

    It "Adds new capabilities to existing agent" {
        $registry = @{
            version = "1"
            last_updated = ""
            agents = @{
                "@dotnet" = @{
                    capabilities = @(
                        @{ id = "redis-caching"; category = "infrastructure" }
                    )
                }
            }
        } | ConvertTo-Json -Depth 10 | ConvertFrom-Json

        $newCap = @(
            [PSCustomObject]@{ id = "circuit-breaker"; category = "resilience"; description = "CB"; evidence = "Polly" }
        )

        $updated = Update-Registry -Registry $registry -Agent "@dotnet" -NewCapabilities $newCap -Pr "342"
        $updated.agents."@dotnet".capabilities.Count | Should -Be 2
        $updated.agents."@dotnet".capabilities[1].id | Should -Be "circuit-breaker"
        $updated.agents."@dotnet".capabilities[1].confidence | Should -Be "medium"
    }

    It "Creates new agent entry if agent not in registry" {
        $registry = @{
            version = "1"
            last_updated = ""
            agents = @{}
        } | ConvertTo-Json | ConvertFrom-Json

        $newCap = @(
            [PSCustomObject]@{ id = "ngrx-signals"; category = "frontend"; description = "Signals"; evidence = "@ngrx/signals" }
        )

        $updated = Update-Registry -Registry $registry -Agent "@angular" -NewCapabilities $newCap -Pr "350"
        $updated.agents."@angular".capabilities.Count | Should -Be 1
        $updated.agents."@angular".capabilities[0].id | Should -Be "ngrx-signals"
    }
}

Describe "Integration: Full Pipeline (Dry Run)" {

    It "Processes a mock diff without creating PR (manual mode)" {
        $mockDiff = @"
+ using StackExchange.Redis;
+ services.AddStackExchangeRedisCache(config => {
+     config.Configuration = "redis:6379";
+ });
+ using Polly;
+ var cb = Policy.Handle<HttpRequestException>()
+     .CircuitBreaker(3, TimeSpan.FromSeconds(30));
+ services.AddPolicyHandler(cb);
"@

        # Load rules (from the real rules file)
        $rules = Load-JsonFile (Join-Path $PSScriptRoot "..\capability-rules.json")

        # Create a temp registry copy
        $tempRegistry = Join-Path $env:TEMP "test-agent-capabilities.json"
        @"
{
  "version": "1",
  "last_updated": "",
  "agents": {}
}
"@ | Set-Content $tempRegistry

        $registry = Load-JsonFile $tempRegistry

        $detected = Detect-Capabilities -Diff $mockDiff -Rules $rules
        $detected.Count | Should -BeGreaterThan 0

        $newCapabilities = Compare-WithRegistry -Detected $detected -Registry $registry -Agent "@dotnet"
        $newCapabilities.Count | Should -Be $detected.Count

        $updated = Update-Registry -Registry $registry -Agent "@dotnet" -NewCapabilities $newCapabilities -Pr "999"
        $updated.agents."@dotnet".capabilities.Count | Should -Be $detected.Count

        # Verify no duplicates on second run
        $detected2 = Detect-Capabilities -Diff $mockDiff -Rules $rules
        $newCapabilities2 = Compare-WithRegistry -Detected $detected2 -Registry $updated -Agent "@dotnet"
        $newCapabilities2.Count | Should -Be 0

        Remove-Item $tempRegistry -ErrorAction SilentlyContinue
    }
}
