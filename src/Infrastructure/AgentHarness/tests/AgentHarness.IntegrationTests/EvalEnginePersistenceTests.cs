using FluentAssertions;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.IntegrationTests;

public class EvalEnginePersistenceTests
{
    [Fact]
    public void EvalSuite_CreateAndProperties_ShouldWork()
    {
        // Arrange & Act
        var suite = EvalSuite.Create("test-suite", "coding", "A test suite", """{"key":"value"}""");

        // Assert
        suite.Should().NotBeNull();
        suite.Id.Should().NotBeEmpty();
        suite.Name.Should().Be("test-suite");
        suite.Domain.Should().Be("coding");
        suite.Description.Should().Be("A test suite");
        suite.DefinitionJson.Should().Be("""{"key":"value"}""");
        suite.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EvalRun_CreateAndComplete_ShouldTrackMetrics()
    {
        // Arrange
        var suite = EvalSuite.Create("suite", "domain", "desc", "{}");

        // Act
        var run = EvalRun.Create(suite.Id, "dotnet", "gpt-4");

        // Assert initial state
        run.Id.Should().NotBeEmpty();
        run.EvalSuiteId.Should().Be(suite.Id);
        run.TargetAgent.Should().Be("dotnet");
        run.TargetModel.Should().Be("gpt-4");
        run.Status.Should().Be(EvalRunStatus.Pending);

        // Act - complete
        run.Complete(0.85, 0.95, 90, """{"results":"ok"}""");

        // Assert completed state
        run.Status.Should().Be(EvalRunStatus.Completed);
        run.PassAt1.Should().Be(0.85);
        run.PassAtK.Should().Be(0.95);
        run.JudgeScoreValue.Should().Be(90);
        run.RawResultJson.Should().Be("""{"results":"ok"}""");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void EvalRun_Fail_ShouldTransitionToFailed()
    {
        // Arrange
        var suite = EvalSuite.Create("suite", "domain", "desc", "{}");
        var run = EvalRun.Create(suite.Id, "dotnet");

        // Act
        run.Fail();

        // Assert
        run.Status.Should().Be(EvalRunStatus.Failed);
        run.CompletedAt.Should().NotBeNull();
        run.PassAt1.Should().BeNull();
    }

    [Fact]
    public void EvalRun_WithoutModel_ShouldAllowNullTargetModel()
    {
        // Arrange & Act
        var suite = EvalSuite.Create("suite", "domain", "desc", "{}");
        var run = EvalRun.Create(suite.Id, "angular");

        // Assert
        run.TargetModel.Should().BeNull();
        run.TargetAgent.Should().Be("angular");
    }
}
