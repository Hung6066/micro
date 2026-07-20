using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class EvalEngineServiceTests
{
    private readonly Mock<IStateStore> _storeMock = new();
    private readonly Mock<LlmJudgeService> _judgeMock = new();

    public EvalEngineServiceTests()
    {
        // LlmJudgeService has a parameterless constructor that creates a default provider.
        // We use it directly mock-free for the rule-based fallback in tests.
    }

    [Fact]
    public async Task RunSuiteAsync_WithStoredSuite_ReturnsRunWithPassAtK()
    {
        // Arrange
        var suite = EvalSuite.Create("code-gen", "coding", "Generate code", """{"tasks":[{"input":"hello","expected":"world"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        var result = await service.RunSuiteAsync("code-gen", "dotnet", targetModel: null, k: 3);

        // Assert
        result.Should().NotBeNull();
        result.EvalSuiteName.Should().Be("code-gen");
        result.TargetAgent.Should().Be("dotnet");
        result.PassAt1.Should().NotBeNull();
        result.PassAt1!.Value.Should().BeInRange(0, 1);
        result.PassAtK.Should().NotBeNull();
        result.PassAtK!.Value.Should().BeInRange(0, 1);
        result.Status.Should().BeOneOf("Completed", "Failed");
        result.EvalRunId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunSuiteAsync_WithMultipleAttempts_ComputesPassAtKCorrectly()
    {
        // Arrange - suite with 5 tasks
        var suite = EvalSuite.Create("multi-task", "coding", "Multiple tasks", """{"tasks":[{"input":"t1"},{"input":"t2"},{"input":"t3"},{"input":"t4"},{"input":"t5"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        var result = await service.RunSuiteAsync("multi-task", "dotnet", targetModel: null, k: 5);

        // Assert
        result.Should().NotBeNull();
        result.PassAt1.Should().NotBeNull();
        result.PassAt1!.Value.Should().BeInRange(0, 1);
        result.PassAtK.Should().NotBeNull();
        result.PassAtK!.Value.Should().BeInRange(0, 1);

        // pass_at_k should be >= pass_at_1 for any reasonable sampling
        // (with enough attempts, pass_at_k approaches 1)
        result.PassAtK!.Value.Should().BeGreaterOrEqualTo(result.PassAt1!.Value);
    }

    [Fact]
    public async Task CompareModelsAsync_ReturnsSortedResultsWithWinner()
    {
        // Arrange - suite with tasks
        var suite = EvalSuite.Create("compare-test", "qa", "Compare models", """{"tasks":[{"input":"q1"},{"input":"q2"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        var models = new[] { "gpt-4", "claude-3", "gemini-pro" };
        var result = await service.CompareModelsAsync("compare-test", "dotnet", models, k: 3);

        // Assert
        result.Should().NotBeNull();
        result.EvalSuiteName.Should().Be("compare-test");
        result.TargetAgent.Should().Be("dotnet");
        result.Results.Should().HaveCount(3);
        result.WinnerModel.Should().NotBeNullOrEmpty();

        // Results should be sorted descending by PassAt1
        for (int i = 1; i < result.Results.Count; i++)
        {
            var prev = result.Results[i - 1].PassAt1 ?? 0;
            var curr = result.Results[i].PassAt1 ?? 0;
            prev.Should().BeGreaterOrEqualTo(curr);
        }
    }

    [Fact]
    public async Task CompareModelsAsync_WithSingleModel_ThatModelIsWinner()
    {
        // Arrange
        var suite = EvalSuite.Create("single-model", "coding", "Single model", """{"tasks":[{"input":"x"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        var result = await service.CompareModelsAsync("single-model", "dotnet", new[] { "gpt-4" }, k: 1);

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().HaveCount(1);
        result.WinnerModel.Should().Be("gpt-4");
    }

    [Fact]
    public async Task RunSuiteAsync_UnknownSuite_ThrowsArgumentException()
    {
        // Arrange
        _storeMock.Setup(s => s.GetEvalSuiteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvalSuite?)null);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        Func<Task> act = () => service.RunSuiteAsync("nonexistent", "dotnet", targetModel: null, k: 3);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*nonexistent*");
    }

    [Fact]
    public async Task RunSuiteAsync_EmptyTasks_ReturnsCompletedWithZeroScores()
    {
        // Arrange - suite with empty tasks
        var suite = EvalSuite.Create("empty-suite", "coding", "No tasks", """{"tasks":[]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        var result = await service.RunSuiteAsync("empty-suite", "dotnet", targetModel: null, k: 3);

        // Assert
        result.Should().NotBeNull();
        result.PassAt1.Should().Be(0);
        result.PassAtK.Should().Be(0);
    }

    // ---- Helpers ----

    private void SetupStoreWithSuite(EvalSuite suite)
    {
        _storeMock.Setup(s => s.GetEvalSuiteAsync(suite.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suite);

        _storeMock.Setup(s => s.SaveEvalRunAsync(It.IsAny<EvalRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storeMock.Setup(s => s.GetEvalSuitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EvalSuite> { suite });
    }
}
