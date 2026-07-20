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

    [Fact]
    public async Task Execute_ReturnsAgentProfileJsonShape()
    {
        // Arrange
        var runs = CreateFakeRuns(out var pipelineRunIds);
        var gates = CreateFakeGates(pipelineRunIds);
        var memories = CreateFakeMemories();

        SetupStore("dotnet", runs, gates, memories);

        var service = new AgentMetricsService(_storeMock.Object);
        var tool = new GetAgentProfileTool(service);

        var parameters = new Dictionary<string, object>
        {
            ["agent_name"] = "dotnet"
        };

        // Act
        var json = await tool.ExecuteAsync(parameters);

        // Assert — should deserialize to AgentProfileDto shape
        var obj = JsonSerializer.Deserialize<JsonElement>(json);
        obj.TryGetProperty("agent_name", out _).Should().BeTrue();
        obj.TryGetProperty("ais_score", out _).Should().BeTrue();
        obj.TryGetProperty("task_completion_rate", out _).Should().BeTrue();
        obj.TryGetProperty("quality_gate_pass_rate", out _).Should().BeTrue();
        obj.TryGetProperty("retry_rate", out _).Should().BeTrue();
        obj.TryGetProperty("confidence_accuracy", out _).Should().BeTrue();
        obj.TryGetProperty("learning_effectiveness", out _).Should().BeTrue();
        obj.TryGetProperty("average_judge_score", out _).Should().BeTrue();
        obj.TryGetProperty("total_runs", out _).Should().BeTrue();
        obj.TryGetProperty("successful_runs", out _).Should().BeTrue();
        obj.TryGetProperty("recent_runs", out _).Should().BeTrue();

        // Verify recent runs is an array
        var recentRuns = obj.GetProperty("recent_runs");
        recentRuns.ValueKind.Should().Be(JsonValueKind.Array);

        if (recentRuns.GetArrayLength() > 0)
        {
            var first = recentRuns[0];
            first.TryGetProperty("agent_run_id", out _).Should().BeTrue();
            first.TryGetProperty("pipeline_run_id", out _).Should().BeTrue();
            first.TryGetProperty("status", out _).Should().BeTrue();
            first.TryGetProperty("confidence_score", out _).Should().BeTrue();
            first.TryGetProperty("started_at", out _).Should().BeTrue();
            first.TryGetProperty("completed_at", out _).Should().BeTrue();
            first.TryGetProperty("duration_seconds", out _).Should().BeTrue();
            first.TryGetProperty("artifact_ref", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Execute_WhenAgentNameMissing_Throws()
    {
        // Arrange
        var storeMock = new Mock<IStateStore>();
        storeMock.Setup(s => s.GetAgentRunsByAgentNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        storeMock.Setup(s => s.GetMemoryEntriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());

        var service = new AgentMetricsService(storeMock.Object);
        var tool = new GetAgentProfileTool(service);

        // Act
        Func<Task> act = () => tool.ExecuteAsync(new Dictionary<string, object>());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*agent_name*");
    }

    // ---- Helpers ----

    private static List<AgentRun> CreateFakeRuns(out List<Guid> pipelineRunIds)
    {
        pipelineRunIds = new List<Guid> { Guid.NewGuid() };

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

    private void SetupStore(string agentName, List<AgentRun> runs, List<QualityGate> gates, List<MemoryEntry> memories)
    {
        _storeMock.Setup(s => s.GetAgentRunsByAgentNameAsync(agentName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(runs);

        _storeMock.Setup(s => s.GetAgentRunsByAgentNameAsync(It.Is<string>(a => a != agentName), It.IsAny<CancellationToken>()))
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
