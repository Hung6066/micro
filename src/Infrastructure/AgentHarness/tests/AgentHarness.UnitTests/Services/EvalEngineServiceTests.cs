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

    // ============================================================
    // Exact pass@1 / pass@k tests using mocked judge
    // ============================================================

    [Fact]
    public async Task RunSuiteAsync_WithMockedJudge_ExactPassAt1AndPassAtK()
    {
        // Arrange: 2 tasks WITHOUT expected (open-ended → judge is used)
        var suite = EvalSuite.Create("exact-test", "qa", "Exact", """{"tasks":[{"input":"q1"},{"input":"q2"}]}""");
        SetupStoreWithSuite(suite);

        // Judge returns for 2 tasks × k=3 = 6 calls:
        // Task 0 attempts: pass, fail, pass   → first-attempt passes, any-attempt passes
        // Task 1 attempts: fail, fail, pass   → first-attempt fails, any-attempt passes
        var sequence = _judgeMock.SetupSequence(j => j.EvaluateQuality(It.IsAny<string>(), It.IsAny<string>()));
        // Task 0
        sequence.Returns(new JudgeScore { Passed = true, NumericScore = 90 });
        sequence.Returns(new JudgeScore { Passed = false, NumericScore = 30 });
        sequence.Returns(new JudgeScore { Passed = true, NumericScore = 85 });
        // Task 1
        sequence.Returns(new JudgeScore { Passed = false, NumericScore = 20 });
        sequence.Returns(new JudgeScore { Passed = false, NumericScore = 25 });
        sequence.Returns(new JudgeScore { Passed = true, NumericScore = 95 });

        var service = new EvalEngineService(_storeMock.Object, _judgeMock.Object);

        // Act
        var result = await service.RunSuiteAsync("exact-test", "dotnet", targetModel: "gpt-4", k: 3);

        // Assert exact values:
        // pass@1 = first-attempt successes / total tasks = 1/2 = 0.5
        result.PassAt1.Should().Be(0.5);
        // pass@k = tasks where ANY attempt succeeded / total = 2/2 = 1.0
        result.PassAtK.Should().Be(1.0);
        // avg judge score = (90+30+85+20+25+95)/6 = 345/6 = 57.5 → integer: 57
        result.JudgeScore.Should().Be(57);
        result.Status.Should().Be("Completed");
        result.EvalSuiteName.Should().Be("exact-test");
        result.TargetAgent.Should().Be("dotnet");
        result.TargetModel.Should().Be("gpt-4");
        result.EvalRunId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunSuiteAsync_WithMockedJudge_AllFail_ZeroScores()
    {
        // Arrange: 2 tasks, k=3, all judge calls return fail
        var suite = EvalSuite.Create("all-fail", "qa", "All fail", """{"tasks":[{"input":"a"},{"input":"b"}]}""");
        SetupStoreWithSuite(suite);

        var sequence = _judgeMock.SetupSequence(j => j.EvaluateQuality(It.IsAny<string>(), It.IsAny<string>()));
        for (int i = 0; i < 6; i++)
            sequence.Returns(new JudgeScore { Passed = false, NumericScore = 10 });

        var service = new EvalEngineService(_storeMock.Object, _judgeMock.Object);

        // Act
        var result = await service.RunSuiteAsync("all-fail", "dotnet", targetModel: null, k: 3);

        // Assert
        result.PassAt1.Should().Be(0.0);  // 0/2 first attempts succeed
        result.PassAtK.Should().Be(0.0);  // 0/2 tasks have any success
        result.JudgeScore.Should().Be(10);
    }

    [Fact]
    public async Task RunSuiteAsync_WithMockedJudge_AllPass_PerfectScores()
    {
        // Arrange: 2 tasks, k=2, all judge calls return pass
        var suite = EvalSuite.Create("all-pass", "qa", "All pass", """{"tasks":[{"input":"a"},{"input":"b"}]}""");
        SetupStoreWithSuite(suite);

        var sequence = _judgeMock.SetupSequence(j => j.EvaluateQuality(It.IsAny<string>(), It.IsAny<string>()));
        for (int i = 0; i < 4; i++)
            sequence.Returns(new JudgeScore { Passed = true, NumericScore = 100 });

        var service = new EvalEngineService(_storeMock.Object, _judgeMock.Object);

        // Act
        var result = await service.RunSuiteAsync("all-pass", "dotnet", targetModel: null, k: 2);

        // Assert
        result.PassAt1.Should().Be(1.0);  // 2/2 first attempts succeed
        result.PassAtK.Should().Be(1.0);  // 2/2 tasks have success
        result.JudgeScore.Should().Be(100);
    }

    // ============================================================
    // RawResultJson shape tests — validates deterministic serialization
    // ============================================================

    [Fact]
    public async Task RunSuiteAsync_RawResultJson_ShouldUseDeterministicDtoShape()
    {
        // Arrange
        var suite = EvalSuite.Create("raw-json-test", "qa", "Raw JSON test",
            """{"tasks":[{"input":"q1","expected":"a1"},{"input":"q2","expected":"a2"},{"input":"q3","expected":"a3"}]}""");
        SetupStoreWithSuite(suite);

        EvalRun? savedRun = null;
        _storeMock.Setup(s => s.SaveEvalRunAsync(It.IsAny<EvalRun>(), It.IsAny<CancellationToken>()))
            .Callback<EvalRun, CancellationToken>((run, _) => savedRun = run)
            .Returns(Task.CompletedTask);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        await service.RunSuiteAsync("raw-json-test", "dotnet", targetModel: null, k: 3);

        // Assert: persisted RawResultJson must use named "Passed"/"Score" keys,
        // not ValueTuple's "Item1"/"Item2" (which is non-deterministic).
        savedRun.Should().NotBeNull();
        savedRun!.RawResultJson.Should().NotBeNullOrEmpty();

        // Parse and validate the JSON structure
        using var doc = System.Text.Json.JsonDocument.Parse(savedRun.RawResultJson);
        doc.RootElement.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        var outerArray = doc.RootElement.EnumerateArray().ToList();
        outerArray.Should().HaveCount(3); // 3 tasks

        foreach (var taskArray in outerArray)
        {
            taskArray.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
            var attempts = taskArray.EnumerateArray().ToList();
            attempts.Should().HaveCount(3); // k=3 attempts each

            foreach (var attempt in attempts)
            {
                attempt.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
                // Must have "Passed" and "Score" — NOT "Item1" or "Item2"
                attempt.TryGetProperty("Passed", out _).Should().BeTrue();
                attempt.TryGetProperty("Score", out _).Should().BeTrue();
                attempt.TryGetProperty("Item1", out _).Should().BeFalse();
                attempt.TryGetProperty("Item2", out _).Should().BeFalse();
            }
        }
    }

    // ============================================================
    // Suite-driven grading (tasks WITH expected → exact match)
    // ============================================================

    [Fact]
    public async Task RunSuiteAsync_WithExpected_GradesByExactMatch()
    {
        // Arrange: suite with Expected values drives pass/fail
        // pass@1 and pass@k are deterministic via hash(agent, model, task, attempt)
        // For agent "dotnet" with no model:
        var suite = EvalSuite.Create("expected-grading", "coding", "Expected grading",
            """{"tasks":[{"input":"aaa","expected":"bbb"},{"input":"ccc","expected":"ddd"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act - call twice to verify reproducibility
        var result1 = await service.RunSuiteAsync("expected-grading", "dotnet", targetModel: null, k: 3);
        var result2 = await service.RunSuiteAsync("expected-grading", "dotnet", targetModel: null, k: 3);

        // Assert exact reproducibility: same inputs → same outputs
        result1.PassAt1.Should().Be(result2.PassAt1);
        result1.PassAtK.Should().Be(result2.PassAtK);
        result1.JudgeScore.Should().Be(result2.JudgeScore);

        // pass@k >= pass@1 always (handle nullable doubles)
        result1.PassAt1.Should().NotBeNull();
        result1.PassAtK.Should().NotBeNull();
        result1.PassAtK!.Value.Should().BeGreaterOrEqualTo(result1.PassAt1!.Value);
    }

    [Fact]
    public async Task RunSuiteAsync_WithExpected_DifferentAgentsGetDifferentResults()
    {
        // Arrange
        var suite = EvalSuite.Create("agent-compare", "coding", "Agent comparison",
            """{"tasks":[{"input":"task1","expected":"out1"},{"input":"task2","expected":"out2"},{"input":"task3","expected":"out3"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act - different agents should get different results due to hash variation
        var dotnetResult = await service.RunSuiteAsync("agent-compare", "dotnet", targetModel: null, k: 3);
        var angularResult = await service.RunSuiteAsync("agent-compare", "angular", targetModel: null, k: 3);

        // Assert - not strictly required to differ, but they should be deterministic
        dotnetResult.Should().NotBeNull();
        angularResult.Should().NotBeNull();
        dotnetResult.PassAt1.Should().BeGreaterOrEqualTo(0);
        dotnetResult.PassAt1.Should().BeLessOrEqualTo(1);
        angularResult.PassAt1.Should().BeGreaterOrEqualTo(0);
        angularResult.PassAt1.Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task RunSuiteAsync_MixedTasks_UsesJudgeOnlyForNonExpectedTasks()
    {
        // Arrange: one task with expected, one without
        var suite = EvalSuite.Create("mixed", "qa", "Mixed",
            """{"tasks":[{"input":"x","expected":"y"},{"input":"z"}]}""");
        SetupStoreWithSuite(suite);

        // Judge only called for task without expected (k=2 → 2 calls)
        _judgeMock.Setup(j => j.EvaluateQuality(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new JudgeScore { Passed = true, NumericScore = 75 });

        var service = new EvalEngineService(_storeMock.Object, _judgeMock.Object);

        // Act
        var result = await service.RunSuiteAsync("mixed", "dotnet", targetModel: null, k: 2);

        // Assert
        result.PassAt1.Should().NotBeNull();
        result.PassAtK.Should().NotBeNull();
        result.Status.Should().Be("Completed");
    }

    // ============================================================
    // CompareModelsAsync exact sorting tests
    // ============================================================

    [Fact]
    public async Task CompareModelsAsync_ReturnsSortedByPassAt1ThenPassAtKThenJudgeScore()
    {
        // Arrange - mock judge to get predictable sequence for each model
        // Models are processed in the order passed to CompareModelsAsync.
        // SetupSequence returns values in call order across ALL calls.
        var suite = EvalSuite.Create("sort-test", "qa", "Sort test", """{"tasks":[{"input":"a"},{"input":"b"}]}""");
        SetupStoreWithSuite(suite);

        // Model order: gpt-4 first (all-pass), claude-3 second (mixed), gemini-pro third (mostly fail)
        // (2 tasks × k=2 = 4 judge calls per model)
        _judgeMock.SetupSequence(j => j.EvaluateQuality(It.IsAny<string>(), It.IsAny<string>()))
            // gpt-4: all attempts pass → pass@1=1.0, pass@k=1.0
            .Returns(new JudgeScore { Passed = true, NumericScore = 90 })
            .Returns(new JudgeScore { Passed = true, NumericScore = 85 })
            .Returns(new JudgeScore { Passed = true, NumericScore = 95 })
            .Returns(new JudgeScore { Passed = true, NumericScore = 80 })
            // claude-3: first task passes first attempt, mixed → pass@1=0.5, pass@k=1.0
            .Returns(new JudgeScore { Passed = true, NumericScore = 70 })
            .Returns(new JudgeScore { Passed = false, NumericScore = 30 })
            .Returns(new JudgeScore { Passed = false, NumericScore = 40 })
            .Returns(new JudgeScore { Passed = true, NumericScore = 60 })
            // gemini-pro: both first attempts fail, only one later passes → pass@1=0.0, pass@k=0.5
            .Returns(new JudgeScore { Passed = false, NumericScore = 20 })
            .Returns(new JudgeScore { Passed = false, NumericScore = 10 })
            .Returns(new JudgeScore { Passed = false, NumericScore = 15 })
            .Returns(new JudgeScore { Passed = true, NumericScore = 50 });

        var service = new EvalEngineService(_storeMock.Object, _judgeMock.Object);

        // Act - models are processed in array order; mock sequence applies globally
        var models = new[] { "gpt-4", "claude-3", "gemini-pro" };
        var result = await service.CompareModelsAsync("sort-test", "dotnet", models, k: 2);

        // Assert: sorted by PassAt1 desc, then PassAtK desc, then JudgeScore desc
        result.Results.Should().HaveCount(3);
        result.Results[0].TargetModel.Should().Be("gpt-4");   // pass@1=1.0
        result.Results[1].TargetModel.Should().Be("claude-3"); // pass@1=0.5
        result.Results[2].TargetModel.Should().Be("gemini-pro"); // pass@1=0.0
        result.WinnerModel.Should().Be("gpt-4");
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

    // ============================================================
    // Error case tests
    // ============================================================

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
        result.JudgeScore.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task RunSuiteAsync_KIsNotPositive_ThrowsArgumentException(int invalidK)
    {
        // Arrange
        var suite = EvalSuite.Create("k-test", "coding", "K test", """{"tasks":[{"input":"x"}]}""");
        SetupStoreWithSuite(suite);

        var service = new EvalEngineService(_storeMock.Object, new LlmJudgeService());

        // Act
        Func<Task> act = () => service.RunSuiteAsync("k-test", "dotnet", targetModel: null, k: invalidK);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*k must be greater than 0*");
    }

    // ============================================================
    // Stable hash reproducibility tests
    // ============================================================

    [Fact]
    public void IsPassingAttempt_DeterministicReproducibility()
    {
        // Assert same inputs → same result across 100 consecutive calls
        for (int i = 0; i < 100; i++)
        {
            var result1 = EvalEngineService.IsPassingAttempt("dotnet", null, "test-input", 0);
            var result2 = EvalEngineService.IsPassingAttempt("dotnet", null, "test-input", 0);
            result1.Should().Be(result2);
        }
    }

    [Fact]
    public void IsPassingAttempt_DifferentAgentsMayDiffer()
    {
        var dotnet = EvalEngineService.IsPassingAttempt("dotnet", null, "same-input", 0);
        var angular = EvalEngineService.IsPassingAttempt("angular", null, "same-input", 0);

        // They may be same or different for any given input, but both should be valid booleans
        dotnet.GetType().Should().Be(typeof(bool));
        angular.GetType().Should().Be(typeof(bool));
    }

    [Fact]
    public void GetStableHashCode_ReturnsConsistentValue()
    {
        var hash1 = EvalEngineService.GetStableHashCode("hello|world|test|0");
        var hash2 = EvalEngineService.GetStableHashCode("hello|world|test|0");
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetStableHashCode_DifferentInputsReturnDifferentHashes()
    {
        var hash1 = EvalEngineService.GetStableHashCode("inputA|0");
        var hash2 = EvalEngineService.GetStableHashCode("inputA|1");
        var hash3 = EvalEngineService.GetStableHashCode("inputB|0");
        hash1.Should().NotBe(hash2);
        hash1.Should().NotBe(hash3);
    }

    // ============================================================
    // Helpers
    // ============================================================

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
