using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Commands;

public class StartPipelineHandlerTests
{
    [Fact]
    public async Task Handle_ShouldCreatePipelineRun_AndStartEngine()
    {
        var engineMock = new Mock<IPipelineEngine>();
        var storeMock = new Mock<IStateStore>();
        storeMock.Setup(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        engineMock.Setup(e => e.StartAsync(It.IsAny<PipelineDag>(), It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>())).ReturnsAsync((PipelineDag d, PipelineRun r, CancellationToken _) => r);

        var handler = new StartPipelineHandler(engineMock.Object, storeMock.Object);
        var command = new StartPipelineCommand("test-workflow", new Dictionary<string, string>(), "user");
        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.WorkflowId.Should().Be("test-workflow");
        storeMock.Verify(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
