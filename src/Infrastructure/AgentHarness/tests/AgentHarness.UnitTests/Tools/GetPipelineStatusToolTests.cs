using System.Text.Json;
using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;
using His.Hope.AgentHarness.Mcp.Tools;

namespace His.Hope.AgentHarness.UnitTests.Tools;

public class GetPipelineStatusToolTests
{
    [Fact]
    public async Task ExecuteAsync_PipelineRunWithMetadata_ShouldExposeMetadataInResponse()
    {
        // Arrange
        var store = new Mock<IStateStore>();

        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        run.AddMetadata("adaptive_risk_checked_at", "2025-01-01T00:00:00Z");
        run.AddMetadata("adaptive_risk_0", "level=Low;score=0.15;Agent has adequate history");
        run.AddMetadata("adaptive_risk_count", "1");

        store.Setup(s => s.GetPipelineRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        store.Setup(s => s.GetAgentRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());
        store.Setup(s => s.GetChildPipelineRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PipelineRun>());

        var tool = new GetPipelineStatusTool(store.Object);
        var parameters = new Dictionary<string, object>
        {
            ["pipeline_run_id"] = run.Id.ToString()
        };

        // Act
        var json = await tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.TryGetProperty("metadata", out var metadataEl).Should().BeTrue(
            "GetPipelineStatusTool must expose metadata in its response");

        metadataEl.ValueKind.Should().Be(JsonValueKind.Object,
            "metadata should be a JSON object/dictionary");

        metadataEl.TryGetProperty("adaptive_risk_checked_at", out _).Should().BeTrue(
            "risk metadata should appear in the status output");
        metadataEl.TryGetProperty("adaptive_risk_0", out _).Should().BeTrue(
            "risk predictions should appear in the status output");
        metadataEl.TryGetProperty("adaptive_risk_count", out _).Should().BeTrue(
            "risk count should appear in the status output");
    }

    [Fact]
    public async Task ExecuteAsync_PipelineRunWithoutMetadata_ShouldReturnEmptyMetadata()
    {
        // Arrange
        var store = new Mock<IStateStore>();
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);

        store.Setup(s => s.GetPipelineRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        store.Setup(s => s.GetAgentRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());
        store.Setup(s => s.GetChildPipelineRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PipelineRun>());

        var tool = new GetPipelineStatusTool(store.Object);
        var parameters = new Dictionary<string, object>
        {
            ["pipeline_run_id"] = run.Id.ToString()
        };

        // Act
        var json = await tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(json);

        // Assert — metadata field should exist but be empty
        doc.RootElement.TryGetProperty("metadata", out var metadataEl).Should().BeTrue();
        var metadataDict = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataEl.GetRawText());
        metadataDict.Should().NotBeNull("metadata field must always be present");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludePipelineMetadataRoundtrip()
    {
        // Arrange — simulate a pipeline run where metadata was added
        var store = new Mock<IStateStore>();

        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);

        // Simulate AdaptiveQualityGates having stored its predictions
        run.AddMetadata("adaptive_risk_checked_at", DateTime.UtcNow.ToString("O"));
        run.AddMetadata("adaptive_risk_count", "2");
        run.AddMetadata("adaptive_risk_0", "level=Low;score=0.12;Agent 'dotnet' adequate");
        run.AddMetadata("adaptive_risk_1", "level=Medium;score=0.45;low AIS (30.0)");

        store.Setup(s => s.GetPipelineRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        store.Setup(s => s.GetAgentRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentRun>());
        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());
        store.Setup(s => s.GetChildPipelineRunsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PipelineRun>());

        var tool = new GetPipelineStatusTool(store.Object);
        var parameters = new Dictionary<string, object>
        {
            ["pipeline_run_id"] = run.Id.ToString()
        };

        // Act
        var json = await tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(json);
        var metadataEl = doc.RootElement.GetProperty("metadata");

        // Assert — deserialize metadata and verify all keys survived
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataEl.GetRawText());

        metadata.Should().ContainKey("adaptive_risk_checked_at");
        metadata.Should().ContainKey("adaptive_risk_count");
        metadata.Should().ContainKey("adaptive_risk_0");
        metadata.Should().ContainKey("adaptive_risk_1");
        metadata!["adaptive_risk_count"].Should().Be("2");
    }
}
