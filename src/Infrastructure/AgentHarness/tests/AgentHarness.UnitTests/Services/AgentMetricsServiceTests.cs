using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class AgentMetricsServiceTests
{
    private readonly Mock<IStateStore> _storeMock = new();
    private readonly Mock<IAgentMetricsRecorder> _metricsMock = new();

    [Fact]
    public async Task GetAgentProfile_ForDotnet_ReturnsScoreBetweenZeroAndOneHundred()
    {
        // Arrange
        var pipelineRuns = CreatePipelineRuns(2);
        var pipelineRunIds = pipelineRuns.Select(pr => pr.Id).ToList();
        var runs = CreateFakeRuns(pipelineRunIds);
        var gates = CreateFakeGates(pipelineRunIds);
        var memories = CreateFakeMemories();

        SetupStore("dotnet", pipelineRuns, runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object, _metricsMock.Object);

        // Act
        var profile = await service.GetAgentProfileAsync("dotnet");

        // Assert
        profile.Should().NotBeNull();
        profile.AgentName.Should().Be("dotnet");
        profile.AisScore.Should().BeInRange(0, 100);
        profile.TotalRuns.Should().Be(runs.Count);
    }

    [Fact]
    public async Task GetAgentProfile_CalculatesMetricsFromFakeRuns()
    {
        // Arrange
        var pipelineRuns = CreatePipelineRuns(2);
        var pipelineRunIds = pipelineRuns.Select(pr => pr.Id).ToList();
        var runs = CreateFakeRuns(pipelineRunIds);
        var gates = CreateFakeGates(pipelineRunIds);
        var memories = CreateFakeMemories();

        SetupStore("dotnet", pipelineRuns, runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object, _metricsMock.Object);
        var successfulCount = runs.Count(r => r.Status == AgentRunStatus.Completed);
        var totalRetries = runs.Sum(r => r.RetryCount);
        var avgMaxRetries = runs.Any() ? runs.Average(r => r.MaxRetries) : 1;
        var expectedRetryRate = 1.0 - Math.Min(totalRetries / (runs.Count * avgMaxRetries), 1.0);
        var passedGates = gates.Count(g => g.Passed);
        var expectedGatePassRate = gates.Count > 0 ? (double)passedGates / gates.Count : 0;
        var expectedTaskCompletion = runs.Count > 0 ? (double)successfulCount / runs.Count : 0;

        // Expected ConfidenceAccuracy:
        // Only completed runs with confidence: r1 (0.95), r3 (0.80), r4 (0.90) = sum 2.65 / 5 total runs
        var expectedConfidenceAccuracy = 2.65 / runs.Count;

        // Act
        var profile = await service.GetAgentProfileAsync("dotnet");

        // Assert
        profile.TaskCompletionRate.Should().BeApproximately(expectedTaskCompletion, 0.01);
        profile.QualityGatePassRate.Should().BeApproximately(expectedGatePassRate, 0.01);
        profile.RetryRate.Should().BeApproximately(expectedRetryRate, 0.01);
        profile.ConfidenceAccuracy.Should().BeApproximately(expectedConfidenceAccuracy, 0.01);
        profile.SuccessfulRuns.Should().Be(successfulCount);
        profile.TotalRuns.Should().Be(runs.Count);
        _metricsMock.Verify(m => m.RecordProfileQuery(), Times.Once);
        _metricsMock.Verify(m => m.RecordAisScore(It.Is<double>(s => s >= 0 && s <= 100)), Times.Once);
    }

    [Fact]
    public async Task GetAgentProfile_ReturnsBoundedHistoryNewestFirst()
    {
        // Arrange
        var pipelineRuns = CreatePipelineRuns(25);
        var pipelineRunIds = pipelineRuns.Select(pr => pr.Id).ToList();
        var runs = CreateManyRuns(pipelineRunIds);
        var gates = new List<QualityGate>();
        var memories = new List<MemoryEntry>();

        SetupStore("angular", pipelineRuns, runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object, _metricsMock.Object);

        // Act
        var profile = await service.GetAgentProfileAsync("angular");

        // Assert
        profile.RecentRuns.Should().NotBeNull();
        profile.RecentRuns.Count.Should().BeLessOrEqualTo(20);

        // Verify newest-first ordering
        for (int i = 1; i < profile.RecentRuns.Count; i++)
        {
            var prev = profile.RecentRuns[i - 1].CompletedAt;
            var curr = profile.RecentRuns[i].CompletedAt;
            (prev >= curr).Should().BeTrue(
                because: $"entry {i - 1} (completed at {prev}) should be newer than entry {i} (completed at {curr})");
        }
    }

    [Fact]
    public async Task GetAgentProfile_CrossAgentGates_DoNotPolluteTargetAgent()
    {
        // Arrange: single pipeline with gates for BOTH "dotnet" and "angular"
        // Only dotnet gates should count toward dotnet's QualityGatePassRate
        var pipelineId = Guid.NewGuid();
        var pipelineRuns = new List<PipelineRun>
        {
            PipelineRun.Create("multi-agent", new Dictionary<string, string>(), "tester")
        };
        // Override the auto-generated Id with our fixed one
        pipelineRuns[0].GetType().GetProperty("Id")!.SetValue(pipelineRuns[0], pipelineId);

        var runs = new List<AgentRun>
        {
            AgentRun.Create(pipelineId, "dotnet", "Build"),
            AgentRun.Create(pipelineId, "angular", "Lint"),
        };
        runs[0].Start(); runs[0].Complete(0.9m, "out1");
        runs[1].Start(); runs[1].Complete(0.9m, "out2");

        // Gates: 3 for dotnet (2 pass, 1 fail) + 2 for angular (both pass)
        var gates = new List<QualityGate>
        {
            QualityGate.Create(pipelineId, "dotnet-build", "build", true),
            QualityGate.Create(pipelineId, "dotnet-test", "test", true),
            QualityGate.Create(pipelineId, "dotnet-security", "security", false),
            QualityGate.Create(pipelineId, "angular-lint", "lint", true),
            QualityGate.Create(pipelineId, "angular-audit", "audit", true),
        };

        // Only return the two dotnet runs when GetAllAgentRunsAsync is called
        _storeMock.Setup(s => s.GetAllAgentRunsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs.Where(r => r.AgentName == "dotnet").ToList());
        _storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        _storeMock.Setup(s => s.GetQualityGatesAsync(pipelineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gates);

        var service = new AgentMetricsService(_storeMock.Object, _metricsMock.Object);

        // Act
        var profile = await service.GetAgentProfileAsync("dotnet");

        // Assert: only dotnet's 3 gates count (2 pass / 3 total = 0.6667), not the 2 angular gates
        profile.QualityGatePassRate.Should().BeApproximately(2.0 / 3.0, 0.01);
    }

    [Fact]
    public async Task GetAgentProfile_WhenNoRuns_ReturnsZeroDefaults()
    {
        // Arrange
        _storeMock.Setup(s => s.GetAllAgentRunsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        _storeMock.Setup(s => s.GetAgentRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        _storeMock.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());
        _storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        var service = new AgentMetricsService(_storeMock.Object, _metricsMock.Object);

        // Act
        var profile = await service.GetAgentProfileAsync("unknown");

        // Assert
        profile.AisScore.Should().Be(0);
        profile.TotalRuns.Should().Be(0);
        profile.RecentRuns.Should().BeEmpty();
    }

    // ---- Helpers ----

    private static List<PipelineRun> CreatePipelineRuns(int count)
    {
        var runs = new List<PipelineRun>();
        for (int i = 0; i < count; i++)
        {
            runs.Add(PipelineRun.Create($"wf-{i}", new Dictionary<string, string>(), "tester"));
        }
        return runs;
    }

    private List<AgentRun> CreateFakeRuns(List<Guid> pipelineRunIds)
    {
        var runs = new List<AgentRun>();

        // Run 1: Success with high confidence
        var r1 = AgentRun.Create(pipelineRunIds[0], "dotnet", "Build frontend");
        r1.Start();
        r1.Complete(0.95m, "build-output-1");
        runs.Add(r1);

        // Run 2: Failed with retries
        var r2 = AgentRun.Create(pipelineRunIds[0], "dotnet", "Run tests");
        r2.Start();
        r2.Fail("Test failure 1");
        r2.Fail("Test failure 2");
        runs.Add(r2);

        // Run 3: Success with moderate confidence
        var r3 = AgentRun.Create(pipelineRunIds[1], "dotnet", "Deploy service");
        r3.Start();
        r3.Complete(0.80m, "deploy-output-1");
        runs.Add(r3);

        // Run 4: Success with high confidence
        var r4 = AgentRun.Create(pipelineRunIds[1], "dotnet", "Run linting");
        r4.Start();
        r4.Complete(0.90m, "lint-output-1");
        runs.Add(r4);

        // Run 5: Failed immediately (no retry)
        var r5 = AgentRun.Create(pipelineRunIds[1], "dotnet", "Security scan");
        r5.Start();
        r5.Fail("Vulnerability found");
        runs.Add(r5);

        return runs;
    }

    private static List<QualityGate> CreateFakeGates(List<Guid> pipelineRunIds)
    {
        // GateIds match the production format: {agentName}-{phase}
        // This ensures the agent-name filter in GetAgentProfileAsync correctly
        // attributes these gates to "dotnet" (and not to cross-agent pipelines).
        return new List<QualityGate>
        {
            QualityGate.Create(pipelineRunIds[0], "dotnet-build", "build", true),
            QualityGate.Create(pipelineRunIds[0], "dotnet-test", "test", true),
            QualityGate.Create(pipelineRunIds[0], "dotnet-lint", "lint", true),
            QualityGate.Create(pipelineRunIds[0], "dotnet-security", "security", false),
            QualityGate.Create(pipelineRunIds[1], "dotnet-deploy", "deploy", true),
            QualityGate.Create(pipelineRunIds[1], "dotnet-integration", "integration", true),
            QualityGate.Create(pipelineRunIds[1], "dotnet-coverage", "coverage", true),
            QualityGate.Create(pipelineRunIds[1], "dotnet-deploy-check", "deploy check", true),
            QualityGate.Create(pipelineRunIds[1], "dotnet-smoke-test", "smoke test", true),
            QualityGate.Create(pipelineRunIds[1], "dotnet-security-scan", "security scan", false),
        };
    }

    private static List<MemoryEntry> CreateFakeMemories()
    {
        return new List<MemoryEntry>
        {
            MemoryEntry.Create("build error CS0117", "build", "dotnet", "Fix missing reference", success: true),
            MemoryEntry.Create("null reference", "runtime", "dotnet", "Add null check", success: true),
            MemoryEntry.Create("test timeout", "test", "dotnet", "Increase timeout", success: true),
            MemoryEntry.Create("config error", "config", "dotnet", "Fix connection string", success: false),
        };
    }

    private List<AgentRun> CreateManyRuns(List<Guid> pipelineRunIds)
    {
        var runs = new List<AgentRun>();
        for (int i = 0; i < pipelineRunIds.Count; i++)
        {
            var run = AgentRun.Create(pipelineRunIds[i], "angular", $"Task {i}");
            run.Start();

            if (i % 3 == 0)
            {
                run.Fail($"Error {i}");
            }
            else
            {
                run.Complete(0.85m + (i * 0.01m), $"ref-{i}");
            }

            runs.Add(run);
        }
        return runs;
    }

    private void SetupStore(string agentName, List<PipelineRun> pipelineRuns, List<AgentRun> runs, List<QualityGate> gates, List<MemoryEntry> memories)
    {
        _storeMock.Setup(s => s.GetAllAgentRunsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        foreach (var pr in pipelineRuns)
        {
            var runsForPipeline = runs.Where(r => r.PipelineRunId == pr.Id).ToList();
            _storeMock.Setup(s => s.GetAgentRunsAsync(pr.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(runsForPipeline);
        }

        var matchedIds = pipelineRuns.Select(pr => pr.Id).ToHashSet();
        _storeMock.Setup(s => s.GetAgentRunsAsync(It.Is<Guid>(id => !matchedIds.Contains(id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());

        // Set up quality gates per pipeline run
        foreach (var gate in gates)
        {
            _storeMock.Setup(s => s.GetQualityGatesAsync(gate.PipelineRunId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(gates.Where(g => g.PipelineRunId == gate.PipelineRunId).ToList());
        }

        // For pipeline runs without explicit gates, return empty
        var gatePipelineIds = gates.Select(g => g.PipelineRunId).ToHashSet();
        _storeMock.Setup(s => s.GetQualityGatesAsync(It.Is<Guid>(id => !gatePipelineIds.Contains(id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        _storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);
    }
}
