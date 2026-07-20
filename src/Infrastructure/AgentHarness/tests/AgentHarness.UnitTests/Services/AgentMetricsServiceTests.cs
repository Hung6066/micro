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

    [Fact]
    public async Task GetAgentProfile_ForDotnet_ReturnsScoreBetweenZeroAndOneHundred()
    {
        // Arrange
        var runs = CreateFakeRuns(out var pipelineRunIds);
        var gates = CreateFakeGates(pipelineRunIds);
        var memories = CreateFakeMemories();

        SetupStore("dotnet", runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object);

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
        var runs = CreateFakeRuns(out var pipelineRunIds);
        var gates = CreateFakeGates(pipelineRunIds);
        var memories = CreateFakeMemories();

        SetupStore("dotnet", runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object);
        var successfulCount = runs.Count(r => r.Status == AgentRunStatus.Completed);
        var totalRetries = runs.Sum(r => r.RetryCount);
        var avgMaxRetries = runs.Any() ? runs.Average(r => r.MaxRetries) : 1;
        var expectedRetryRate = 1.0 - Math.Min(totalRetries / (runs.Count * avgMaxRetries), 1.0);
        var passedGates = gates.Count(g => g.Passed);
        var expectedGatePassRate = gates.Count > 0 ? (double)passedGates / gates.Count : 0;
        var expectedTaskCompletion = runs.Count > 0 ? (double)successfulCount / runs.Count : 0;

        // Act
        var profile = await service.GetAgentProfileAsync("dotnet");

        // Assert
        profile.TaskCompletionRate.Should().BeApproximately(expectedTaskCompletion, 0.01);
        profile.QualityGatePassRate.Should().BeApproximately(expectedGatePassRate, 0.01);
        profile.RetryRate.Should().BeApproximately(expectedRetryRate, 0.01);
        profile.SuccessfulRuns.Should().Be(successfulCount);
        profile.TotalRuns.Should().Be(runs.Count);
    }

    [Fact]
    public async Task GetAgentProfile_ReturnsBoundedHistoryNewestFirst()
    {
        // Arrange
        var runs = CreateManyRuns(25);
        var gates = new List<QualityGate>();
        var memories = new List<MemoryEntry>();

        SetupStore("angular", runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object);

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
    public async Task GetAgentProfile_WhenNoRuns_ReturnsZeroDefaults()
    {
        // Arrange
        _storeMock.Setup(s => s.GetAgentRunsByAgentNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        _storeMock.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());
        _storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        var service = new AgentMetricsService(_storeMock.Object);

        // Act
        var profile = await service.GetAgentProfileAsync("unknown");

        // Assert
        profile.AisScore.Should().Be(0);
        profile.TotalRuns.Should().Be(0);
        profile.RecentRuns.Should().BeEmpty();
    }

    // ---- Helpers ----

    private List<AgentRun> CreateFakeRuns(out List<Guid> pipelineRunIds)
    {
        pipelineRunIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

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
        return new List<QualityGate>
        {
            QualityGate.Create(pipelineRunIds[0], "gate-1", "build", true),
            QualityGate.Create(pipelineRunIds[0], "gate-2", "test", true),
            QualityGate.Create(pipelineRunIds[0], "gate-3", "lint", true),
            QualityGate.Create(pipelineRunIds[0], "gate-4", "security", false),
            QualityGate.Create(pipelineRunIds[1], "gate-5", "build", true),
            QualityGate.Create(pipelineRunIds[1], "gate-6", "test", true),
            QualityGate.Create(pipelineRunIds[1], "gate-7", "integration", true),
            QualityGate.Create(pipelineRunIds[1], "gate-8", "coverage", true),
            QualityGate.Create(pipelineRunIds[1], "gate-9", "deploy", true),
            QualityGate.Create(pipelineRunIds[1], "gate-10", "security", false),
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

    private List<AgentRun> CreateManyRuns(int count)
    {
        var runs = new List<AgentRun>();
        for (int i = 0; i < count; i++)
        {
            var pid = Guid.NewGuid();
            var run = AgentRun.Create(pid, "angular", $"Task {i}");
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

    private void SetupStore(string agentName, List<AgentRun> runs, List<QualityGate> gates, List<MemoryEntry> memories)
    {
        _storeMock.Setup(s => s.GetAgentRunsByAgentNameAsync(agentName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        // Set up quality gates per pipeline run
        foreach (var gate in gates)
        {
            _storeMock.Setup(s => s.GetQualityGatesAsync(gate.PipelineRunId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(gates.Where(g => g.PipelineRunId == gate.PipelineRunId).ToList());
        }

        // For pipeline runs without explicit gates, return empty
        _storeMock.Setup(s => s.GetQualityGatesAsync(It.Is<Guid>(id => !gates.Any(g => g.PipelineRunId == id)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        _storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);
    }
}
