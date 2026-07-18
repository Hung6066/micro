using FluentAssertions;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.IntegrationTests;

public class PipelineEndToEndTests
{
    [Fact]
    public async Task FullPipeline_StartToComplete_ShouldTransitionThroughAllPhases()
    {
        // This test validates the domain models work correctly
        var run = His.Hope.AgentHarness.Core.Models.PipelineRun.Create("test-workflow", new(), "integration-test");

        run.Status.Should().Be(PipelineStatus.Pending);
        run.TransitionTo(PipelineStatus.Running);
        run.Status.Should().Be(PipelineStatus.Running);
        run.TransitionTo(PipelineStatus.Completed);
        run.Status.Should().Be(PipelineStatus.Completed);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangeScopeAnalyzer_DetectsMigrationChanges()
    {
        var analyzer = new His.Hope.AgentHarness.Application.Services.ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "src/Services/PatientService/Migrations/001_init.cs" });
        scope.HasMigrationChanges.Should().BeTrue();
    }

    [Fact]
    public void ChangeScopeAnalyzer_DetectsProtoChanges()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "src/Shared/Protos/patient.proto" });
        scope.HasProtoChanges.Should().BeTrue();
    }

    [Fact]
    public void ChangeScopeAnalyzer_DetectsSecurityChanges()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "src/Services/IdentityService/Auth/LoginHandler.cs" });
        scope.HasSecurityChanges.Should().BeTrue();
    }

    [Fact]
    public void ChangeScopeAnalyzer_MapsBackendChangesToDotnet()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "src/Services/PatientService/PatientService.Api/Program.cs" });
        scope.TriggeredAgents.Should().Contain("dotnet");
        scope.TriggeredAgents.Should().Contain("dba");
    }

    [Fact]
    public void ChangeScopeAnalyzer_MapsK8sChangesToDevops()
    {
        var analyzer = new ChangeScopeAnalyzer();
        var scope = analyzer.Analyze(new[] { "k8s/agent-harness/deployment.yaml" });
        scope.TriggeredAgents.Should().Contain("devops");
    }

    [Fact]
    public void ErrorClassifier_ClassifiesCompilationError()
    {
        var classifier = new His.Hope.AgentHarness.Application.Services.ErrorClassifier();
        var category = classifier.Classify("error CS0246: missing type");
        category.Should().Be(ErrorCategory.CompilationError);
    }

    [Fact]
    public void ErrorClassifier_ClassifiesTestFailure()
    {
        var classifier = new ErrorClassifier();
        var category = classifier.Classify("FAILED test_billing_calculation");
        category.Should().Be(ErrorCategory.TestFailure);
    }

    [Fact]
    public void ErrorClassifier_ClassifiesContractViolation()
    {
        var classifier = new ErrorClassifier();
        var category = classifier.Classify("buf breaking change detected in proto");
        category.Should().Be(ErrorCategory.ContractViolation);
    }

    [Fact]
    public void ErrorClassifier_ClassifiesKnownGotcha()
    {
        var classifier = new ErrorClassifier();
        var category = classifier.Classify("PermissionGuard: take(1) bug detected");
        category.Should().Be(ErrorCategory.KnownGotcha);
    }

    [Fact]
    public void ErrorClassifier_ClassifiesInfrastructureError()
    {
        var classifier = new ErrorClassifier();
        var category = classifier.Classify("connection refused to CockroachDB");
        category.Should().Be(ErrorCategory.InfrastructureError);
    }

    [Fact]
    public void ErrorClassifier_UnknownPattern_ReturnsUnknown()
    {
        var classifier = new ErrorClassifier();
        var category = classifier.Classify("random semantic error in business logic");
        category.Should().Be(ErrorCategory.Unknown);
    }

    [Fact]
    public void ErrorClassifier_IsAutoFixable_ReturnsExpectedResults()
    {
        var classifier = new ErrorClassifier();
        classifier.IsAutoFixable(ErrorCategory.CompilationError).Should().BeTrue();
        classifier.IsAutoFixable(ErrorCategory.TestFailure).Should().BeTrue();
        classifier.IsAutoFixable(ErrorCategory.ContractViolation).Should().BeTrue();
        classifier.IsAutoFixable(ErrorCategory.KnownGotcha).Should().BeTrue();
        classifier.IsAutoFixable(ErrorCategory.QualityGateFailure).Should().BeTrue();
        classifier.IsAutoFixable(ErrorCategory.InfrastructureError).Should().BeFalse();
        classifier.IsAutoFixable(ErrorCategory.LogicError).Should().BeFalse();
        classifier.IsAutoFixable(ErrorCategory.Unknown).Should().BeFalse();
    }

    [Fact]
    public void LoopEngineer_GivesUpAfterMaxIterations()
    {
        var engine = new His.Hope.AgentHarness.Application.Services.LoopEngineer(
            new His.Hope.AgentHarness.Application.Services.ErrorClassifier(),
            new His.Hope.AgentHarness.Application.Services.ConfidenceScorer());
        var context = new His.Hope.AgentHarness.Core.Interfaces.LoopContext
        {
            FailedGates = new(),
            PreviousIteration = 5
        };
        var result = engine.AnalyzeAndFixAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        result.Outcome.Should().Be(FixOutcome.GiveUp);
    }

    [Fact]
    public void LoopEngineer_EscalatesOnInfrastructureError()
    {
        var engine = new LoopEngineer(new ErrorClassifier(), new ConfidenceScorer());
        var context = new LoopContext
        {
            FailedGates = new List<QualityGate>
            {
                QualityGate.Create(Guid.NewGuid(), Guid.NewGuid(), "db-connection", "DB Connection", GateSeverity.Block)
            },
            PreviousIteration = 0
        };
        context.FailedGates[0].MarkFailed("connection refused to CockroachDB on port 26257");
        var result = engine.AnalyzeAndFixAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        result.Outcome.Should().Be(FixOutcome.Escalated);
    }

    [Fact]
    public void LoopEngineer_AutoFixesCompilationError()
    {
        var engine = new LoopEngineer(new ErrorClassifier(), new ConfidenceScorer());
        var context = new LoopContext
        {
            FailedGates = new List<QualityGate>
            {
                QualityGate.Create(Guid.NewGuid(), Guid.NewGuid(), "dotnet-build", "DotNet Build", GateSeverity.Block)
            },
            PreviousIteration = 0
        };
        context.FailedGates[0].MarkFailed("error CS0246: The type or namespace name 'MissingType' could not be found");
        var result = engine.AnalyzeAndFixAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        result.Outcome.Should().Be(FixOutcome.AutoFixed);
        result.Changes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QualityGate_CreateAndFail_ShouldTrackCorrectly()
    {
        var pipelineId = Guid.NewGuid();
        var gate = QualityGate.Create(Guid.NewGuid(), pipelineId, "unit-tests", "Unit Tests", GateSeverity.Block);
        gate.Passed.Should().BeTrue();
        gate.MarkFailed("3 of 15 tests failed");
        gate.Passed.Should().BeFalse();
        gate.Output.Should().Be("3 of 15 tests failed");
        gate.EvaluatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ConfidenceScorer_WithAllSignals_ReturnsHighConfidence()
    {
        var scorer = new ConfidenceScorer();
        var score = scorer.Calculate(new ErrorContext
        {
            MatchesKnownPattern = true,
            ChangeSize = ChangeSize.Small,
            HasSucceededBefore = true,
            IsReversible = true,
            TouchesSecurityBoundary = false
        });
        score.IsAutoFixable.Should().BeTrue();
        score.Value.Should().BeGreaterOrEqualTo(0.8m);
    }

    [Fact]
    public void ConfidenceScorer_WithNoSignals_ReturnsLowConfidence()
    {
        var scorer = new ConfidenceScorer();
        var score = scorer.Calculate(new ErrorContext
        {
            MatchesKnownPattern = false,
            ChangeSize = ChangeSize.Large,
            HasSucceededBefore = false,
            IsReversible = false,
            TouchesSecurityBoundary = true
        });
        score.IsAutoFixable.Should().BeFalse();
        score.Value.Should().BeLessThan(0.8m);
    }

    [Fact]
    public void PipelineRun_TransitionFromCancelled_Throws()
    {
        var run = PipelineRun.Create("test-workflow", new(), "integration-test");
        run.TransitionTo(PipelineStatus.Cancelled);
        Action act = () => run.TransitionTo(PipelineStatus.Running);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot transition a cancelled pipeline.");
    }

    [Fact]
    public void PipelineRun_Timeout_ShouldBeDetected()
    {
        var run = PipelineRun.Create("test-workflow", new(), "integration-test");
        run.SetTimeout(TimeSpan.FromMilliseconds(-1));
        run.IsTimedOut().Should().BeTrue();
    }

    [Fact]
    public void BackpressureController_ExceedsLimit_Throws429()
    {
        var controller = new BackpressureController();
        // Fill pipeline queue
        for (int i = 0; i < 10; i++)
        {
            controller.EnsureCapacity();
        }
        Action act = () => controller.EnsureCapacity();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("HTTP 429*");
    }

    [Fact]
    public void BackpressureController_ReleasePipeline_FreesSlot()
    {
        var controller = new BackpressureController();
        controller.EnsureCapacity();
        controller.ActivePipelineCount.Should().Be(1);
        controller.ReleasePipeline();
        controller.ActivePipelineCount.Should().Be(0);
    }

    [Fact]
    public void BackpressureController_AgentTracking_WorksCorrectly()
    {
        var controller = new BackpressureController();
        var tracked = controller.TryTrackAgent();
        tracked.Should().BeTrue();
        controller.ActiveAgentCount.Should().Be(1);
        controller.ReleaseAgent();
        controller.ActiveAgentCount.Should().Be(0);
    }

    [Fact]
    public void LoopEngineer_SafetyFence_BlocksRestrictedPaths()
    {
        var engine = new LoopEngineer(new ErrorClassifier(), new ConfidenceScorer());
        var context = new LoopContext
        {
            FailedGates = new List<QualityGate>
            {
                QualityGate.Create(Guid.NewGuid(), Guid.NewGuid(), "secret-scan", "Secret Scan", GateSeverity.Block)
            },
            PreviousIteration = 0
        };
        context.FailedGates[0].MarkFailed("Found hardcoded secret in vault/production/secrets.yaml");
        var result = engine.AnalyzeAndFixAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        result.Outcome.Should().Be(FixOutcome.Escalated);
        result.EscalationReason.Should().Contain("Safety fence");
    }

    [Fact]
    public void PipelineDag_BuildAndTraverse_ShouldWork()
    {
        var dag = new PipelineDag("test-pipeline");
        var planNode = dag.AddNode("plan", PipelinePhase.Plan);
        var implNode = dag.AddNode("dotnet", PipelinePhase.Implement);
        dag.AddDependency(implNode.Id, planNode.Id);

        dag.Nodes.Should().HaveCount(2);
        dag.GetPhaseNodes(PipelinePhase.Plan).Should().Contain(n => n.AgentName == "plan");
        dag.GetPhaseNodes(PipelinePhase.Implement).Should().Contain(n => n.AgentName == "dotnet");
        dag.Edges.Should().HaveCount(1);
    }
}
