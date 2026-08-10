using System.Text.Json;
using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Mcp.Tools;

namespace His.Hope.AgentHarness.UnitTests.Tools;

public class GetAgentProfileToolTests
{
    private readonly Mock<IStateStore> _storeMock = new();
    private readonly Mock<IAgentMetricsRecorder> _recorderMock = new();

    [Fact]
    public async Task Execute_ReturnsAgentProfileJsonShape()
    {
        // Arrange
        var pipelineRuns = new List<PipelineRun>
        {
            PipelineRun.Create("wf-1", new Dictionary<string, string>(), "tester")
        };
        var pipelineRunIds = pipelineRuns.Select(pr => pr.Id).ToList();
        var runs = CreateFakeRuns(pipelineRunIds);
        var gates = CreateFakeGates(pipelineRunIds);
        var memories = CreateFakeMemories();

        SetupStore("dotnet", pipelineRuns, runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object, _recorderMock.Object);
        var tool = new GetAgentProfileTool(service);

        var parameters = new Dictionary<string, object>
        {
            ["agent_name"] = "dotnet"
        };

        // Act
        var json = await tool.ExecuteAsync(parameters);

        // Assert — should deserialize to AgentProfileDto shape (camelCase)
        var obj = JsonSerializer.Deserialize<JsonElement>(json);
        obj.TryGetProperty("agentName", out _).Should().BeTrue();
        obj.TryGetProperty("aisScore", out _).Should().BeTrue();
        obj.TryGetProperty("taskCompletionRate", out _).Should().BeTrue();
        obj.TryGetProperty("qualityGatePassRate", out _).Should().BeTrue();
        obj.TryGetProperty("retryRate", out _).Should().BeTrue();
        obj.TryGetProperty("confidenceAccuracy", out _).Should().BeTrue();
        obj.TryGetProperty("learningEffectiveness", out _).Should().BeTrue();
        obj.TryGetProperty("averageJudgeScore", out _).Should().BeTrue();
        obj.TryGetProperty("totalRuns", out _).Should().BeTrue();
        obj.TryGetProperty("successfulRuns", out _).Should().BeTrue();
        obj.TryGetProperty("recentRuns", out _).Should().BeTrue();

        // Verify recent runs is an array
        var recentRuns = obj.GetProperty("recentRuns");
        recentRuns.ValueKind.Should().Be(JsonValueKind.Array);

        if (recentRuns.GetArrayLength() > 0)
        {
            var first = recentRuns[0];
            first.TryGetProperty("agentRunId", out _).Should().BeTrue();
            first.TryGetProperty("pipelineRunId", out _).Should().BeTrue();
            first.TryGetProperty("status", out _).Should().BeTrue();
            first.TryGetProperty("confidenceScore", out _).Should().BeTrue();
            first.TryGetProperty("startedAt", out _).Should().BeTrue();
            first.TryGetProperty("completedAt", out _).Should().BeTrue();
            first.TryGetProperty("durationSeconds", out _).Should().BeTrue();
            first.TryGetProperty("artifactRef", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Execute_WhenAgentNameMissing_Throws()
    {
        // Arrange
        var storeMock = new Mock<IStateStore>();
        storeMock.Setup(s => s.GetAllAgentRunsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        storeMock.Setup(s => s.GetAgentRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        var service = new AgentMetricsService(storeMock.Object, _recorderMock.Object);
        var tool = new GetAgentProfileTool(service);

        // Act
        Func<Task> act = () => tool.ExecuteAsync(new Dictionary<string, object>());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*agent_name*");
    }

    // ---- Helpers ----

    private static List<AgentRun> CreateFakeRuns(List<Guid> pipelineRunIds)
    {
        var r1 = AgentRun.Create(pipelineRunIds[0], "dotnet", "Test task");
        r1.Start();
        r1.Complete(0.95m, "ref-1");

        return new List<AgentRun> { r1 };
    }

    private static List<QualityGate> CreateFakeGates(List<Guid> pipelineRunIds)
    {
        return new List<QualityGate>
        {
            QualityGate.Create(pipelineRunIds[0], "gate-1", "build", true),
            QualityGate.Create(pipelineRunIds[0], "gate-2", "test", true),
        };
    }

    private static List<MemoryEntry> CreateFakeMemories()
    {
        return new List<MemoryEntry>
        {
            MemoryEntry.Create("build error", "build", "dotnet", "Fix it", success: true),
        };
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

        foreach (var gate in gates)
        {
            _storeMock.Setup(s => s.GetQualityGatesAsync(gate.PipelineRunId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(gates.Where(g => g.PipelineRunId == gate.PipelineRunId).ToList());
        }

        _storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);
    }
}
