using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Commands;

public class StartPipelineHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePipelineRun_AndStartEngine()
    {
        var storeMock = new Mock<IStateStore>();
        storeMock.Setup(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new StartPipelineHandler(
            storeMock.Object,
            new WorkflowLoader("workflows"),
            new ChangeScopeAnalyzer(),
            new ConditionalDagBuilder());
        var command = new StartPipelineCommand("test-workflow", new Dictionary<string, string>(), "user");
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.WorkflowId.Should().Be("test-workflow");
        storeMock.Verify(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithChangedFiles_ShouldCreateScopedDagMetadata()
    {
        var storeMock = new Mock<IStateStore>();
        storeMock.Setup(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new StartPipelineHandler(
            storeMock.Object,
            new WorkflowLoader("workflows"),
            new ChangeScopeAnalyzer(),
            new ConditionalDagBuilder());
        var command = new StartPipelineCommand(
            "scoped-workflow",
            new Dictionary<string, string>
            {
                ["changed_files"] = "[\"src/Frontend/his-hope-app/src/app/app.config.ts\"]"
            },
            "user");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Metadata.Should().ContainKey("scope_triggered_agents");
        result.Metadata["scope_triggered_agents"].Should().Contain("angular");
        result.Metadata.Should().ContainKey("resolved_tasks");
    }
}
