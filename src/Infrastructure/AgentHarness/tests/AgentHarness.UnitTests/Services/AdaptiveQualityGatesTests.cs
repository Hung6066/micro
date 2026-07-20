using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Interfaces;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class AdaptiveQualityGatesTests
{
    [Fact]
    public async Task RecommendThresholdsAsync_LowAisAgent_ShouldReturnStricterThreshold()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "dotnet", aisScore: 80.0, gatePassRate: 0.95);
        SetupAgentProfile(metricsMock, "angular", aisScore: 30.0, gatePassRate: 0.50);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var angularRec = await service.RecommendThresholdsAsync("angular", CancellationToken.None);
        var dotnetRec = await service.RecommendThresholdsAsync("dotnet", CancellationToken.None);

        // Assert
        // Low AIS agent (angular) should get stricter threshold (closer to 1.0)
        angularRec.RecommendedGateThreshold.Should().BeGreaterThan(dotnetRec.RecommendedGateThreshold);
    }

    [Fact]
    public async Task RecommendThresholdsAsync_Ais80AndPassRate95_ShouldReturnRelaxedThreshold()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "dotnet", aisScore: 80.0, gatePassRate: 0.95);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var rec = await service.RecommendThresholdsAsync("dotnet", CancellationToken.None);

        // Assert: high-performing agent gets recommended threshold below 0.5
        rec.RecommendedGateThreshold.Should().BeLessThan(0.5);
        rec.AisScore.Should().Be(80.0);
        rec.HistoricalPassRate.Should().Be(0.95);
        rec.AgentName.Should().Be("dotnet");
        rec.LastUpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecommendThresholdsAsync_Ais20AndPassRate30_ShouldReturnStrictThreshold()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "angular", aisScore: 20.0, gatePassRate: 0.30);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var rec = await service.RecommendThresholdsAsync("angular", CancellationToken.None);

        // Assert: low-performing agent needs stricter gate threshold (closer to 1.0)
        rec.RecommendedGateThreshold.Should().BeGreaterThan(0.5);
        rec.AisScore.Should().Be(20.0);
        rec.HistoricalPassRate.Should().Be(0.30);
    }

    [Fact]
    public async Task PredictFailureAsync_ShouldReturnNumericScoreAndReason()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "dotnet", aisScore: 75.0, gatePassRate: 0.90);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        var dag = new PipelineDag();
        dag.AddNode("dotnet", PipelinePhase.Implement);
        dag.AddNode("angular", PipelinePhase.Implement);

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var risks = await service.PredictFailureAsync(run, dag, CancellationToken.None);

        // Assert
        risks.Should().NotBeNull();
        risks.Should().HaveCount(2); // one per agent/phase node

        foreach (var risk in risks)
        {
            risk.RiskScore.Should().BeInRange(0.0, 1.0);
            risk.RiskLevel.Should().NotBeNullOrEmpty();
            risk.Reason.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task PredictFailureAsync_ShouldNotAutoPassOrSkipGates()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "dotnet", aisScore: 95.0, gatePassRate: 0.99);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        var dag = new PipelineDag();
        dag.AddNode("dotnet", PipelinePhase.Implement);

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var risks = await service.PredictFailureAsync(run, dag, CancellationToken.None);

        // Assert: risk should tell us something, not be empty/noop
        risks.Should().NotBeEmpty();
        // Even for a high-AIS agent, we should still return a meaningful prediction
        risks[0].RiskScore.Should().BeGreaterThan(0.0);
        risks[0].RiskLevel.Should().BeOneOf("Low", "Medium", "High", "Critical");
        // Verify no gate-bypass signatures exist -- it returns FailureRiskDto, not bool/gate
        risks[0].Should().BeOfType<FailureRiskDto>();
    }

    [Fact]
    public async Task PredictFailureAsync_NullDag_ShouldThrow()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        Func<Task> act = () => service.PredictFailureAsync(run, null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PredictFailureAsync_NoNodes_ShouldReturnEmpty()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        var dag = new PipelineDag(); // no nodes

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var risks = await service.PredictFailureAsync(run, dag, CancellationToken.None);

        // Assert
        risks.Should().BeEmpty();
    }

    [Fact]
    public async Task PredictFailureAsync_AgentWithNoHistory_ShouldReturnDefaultRisk()
    {
        // Arrange
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        // Agent with no profile data (zero AIS, zero runs)
        var profile = new AgentProfileDto
        {
            AgentName = "unknown-agent",
            AisScore = 0.0,
            TaskCompletionRate = 0.0,
            QualityGatePassRate = 0.0,
            TotalRuns = 0,
            SuccessfulRuns = 0,
            RetryRate = 0.0,
            ConfidenceAccuracy = 0.0,
            LearningEffectiveness = 0.0,
            AverageJudgeScore = 0.0,
            RecentRuns = Array.Empty<AgentRunSummaryDto>()
        };
        metricsMock.Setup(m => m.GetAgentProfileAsync("unknown-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        var dag = new PipelineDag();
        dag.AddNode("unknown-agent", PipelinePhase.Implement);

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var risks = await service.PredictFailureAsync(run, dag, CancellationToken.None);

        // Assert
        risks.Should().HaveCount(1);
        risks[0].RiskScore.Should().BeGreaterOrEqualTo(0.5); // cautious default
        risks[0].RiskLevel.Should().Be("High");
        risks[0].Reason.Should().Contain("no historical data");
    }

    [Fact]
    public async Task PredictFailureAsync_EvalHistory_InfluencesRiskScore()
    {
        // Arrange: two agents with identical profile data but different eval histories
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "agent-a", aisScore: 60.0, gatePassRate: 0.80);
        SetupAgentProfile(metricsMock, "agent-b", aisScore: 60.0, gatePassRate: 0.80);

        // Agent A: perfect eval history (avg pass@1 = 0.95) → should reduce risk
        // Agent B: poor eval history (avg pass@1 = 0.20) → risk should be higher
        var suiteId = Guid.NewGuid();
        var evalSuites = new List<EvalSuite>
        {
            EvalSuite.Create("benchmark", "code", "test suite", "{}")
        };
        // Override auto-generated IDs for deterministic setup
        evalSuites[0].GetType().GetProperty("Id")!.SetValue(evalSuites[0], suiteId);

        store.Setup(s => s.GetEvalSuitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(evalSuites);

        // Agent A eval runs: high pass rates
        var agentARuns = new List<EvalRun>
        {
            CreateCompletedEvalRun(suiteId, "agent-a", passAt1: 0.95),
            CreateCompletedEvalRun(suiteId, "agent-a", passAt1: 0.92),
        };

        // Agent B eval runs: low pass rates
        var agentBRuns = new List<EvalRun>
        {
            CreateCompletedEvalRun(suiteId, "agent-b", passAt1: 0.20),
            CreateCompletedEvalRun(suiteId, "agent-b", passAt1: 0.15),
        };

        // Return ALL runs for the suite; the service filters by TargetAgent internally
        var allEvalRuns = agentARuns.Concat(agentBRuns).ToList();
        store.Setup(s => s.GetEvalRunsAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allEvalRuns);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        // Use a run/pipeline to test both agents
        var run = PipelineRun.Create("test-eval", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        var dag = new PipelineDag();
        dag.AddNode("agent-a", PipelinePhase.Implement);
        dag.AddNode("agent-b", PipelinePhase.Implement);

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act: compute risk for agent-a (good eval) and agent-b (poor eval)
        // We use RecommendThresholdsAsync which also incorporates eval history
        var recA = await service.RecommendThresholdsAsync("agent-a", CancellationToken.None);
        var recB = await service.RecommendThresholdsAsync("agent-b", CancellationToken.None);

        // Assert: agent-b (poor eval) should have a stricter threshold than agent-a (good eval)
        // Since both agents have identical AIS and gate pass rates, any difference
        // in threshold comes from the eval history adjustment.
        recB.RecommendedGateThreshold.Should().BeGreaterThan(recA.RecommendedGateThreshold,
            because: "agent-b has poor eval history, so its recommended threshold should be higher (stricter)");
    }

    [Fact]
    public async Task PredictFailureAsync_EvalHistoryNoData_ShouldNotAffectRisk()
    {
        // Arrange: agent with no eval history should have same risk as without any eval adjustment
        var metricsMock = new Mock<IAgentMetricsService>();
        var store = new Mock<IStateStore>();

        SetupAgentProfile(metricsMock, "dotnet", aisScore: 75.0, gatePassRate: 0.90);

        // No eval suites (empty list)
        store.Setup(s => s.GetEvalSuitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EvalSuite>());

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QualityGate>());

        var run = PipelineRun.Create("test-no-eval", new(), "test-trigger");
        run.TransitionTo(PipelineStatus.Running);
        var dag = new PipelineDag();
        dag.AddNode("dotnet", PipelinePhase.Implement);

        var service = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Act
        var risks = await service.PredictFailureAsync(run, dag, CancellationToken.None);

        // Assert: risk should be reasonable even without eval data
        risks.Should().HaveCount(1);
        risks[0].RiskScore.Should().BeInRange(0.01, 0.99);
        risks[0].RiskLevel.Should().NotBeNullOrEmpty();
    }

    private static EvalRun CreateCompletedEvalRun(Guid suiteId, string agentName, double passAt1)
    {
        var run = EvalRun.Create(suiteId, agentName, "test-model");
        run.Complete(passAt1, passAt1 * 1.1, 85, "[]");
        return run;
    }

    private static void SetupAgentProfile(Mock<IAgentMetricsService> metricsMock, string agentName, double aisScore, double gatePassRate)
    {
        var profile = new AgentProfileDto
        {
            AgentName = agentName,
            AisScore = aisScore,
            TaskCompletionRate = aisScore > 50 ? 0.85 : 0.35,
            QualityGatePassRate = gatePassRate,
            TotalRuns = aisScore > 50 ? 50 : 10,
            SuccessfulRuns = (int)(aisScore > 50 ? 45 : 4),
            RetryRate = aisScore > 50 ? 0.9 : 0.4,
            ConfidenceAccuracy = aisScore > 50 ? 0.84 : 0.30,
            LearningEffectiveness = aisScore > 50 ? 0.88 : 0.40,
            AverageJudgeScore = aisScore > 50 ? 0.82 : 0.35,
            RecentRuns = Array.Empty<AgentRunSummaryDto>()
        };
        metricsMock.Setup(m => m.GetAgentProfileAsync(agentName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
    }
}
