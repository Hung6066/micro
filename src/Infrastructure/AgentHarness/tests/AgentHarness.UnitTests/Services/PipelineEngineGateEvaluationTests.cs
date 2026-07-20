using FluentAssertions;
using Moq;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Interfaces;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

/// <summary>
/// Verifies that PipelineEngine's quality gate evaluation remains independent
/// of advisory risk metadata — gates are never bypassed or auto-passed.
/// </summary>
public class PipelineEngineGateEvaluationTests
{
    [Fact]
    public async Task StartAsync_FailedAgent_ShouldCreateFailedGatesAndBlockPipeline()
    {
        // Arrange
        var pipelineRunId = Guid.NewGuid();
        var dispatcher = new Mock<IAgentDispatcher>();
        var store = new Mock<IStateStore>();
        var eventBus = new Mock<IEventBus>();
        var backpressure = new BackpressureController(100, 100);
        var costTracker = new CostTracker();
        var loopEngineer = new Mock<ILoopEngineer>();
        var metricsMock = new Mock<IAgentMetricsService>();

        // Setup agent profile so adaptive gates have data to work with
        SetupAgentProfile(metricsMock, "dotnet", aisScore: 80.0, gatePassRate: 0.95);

        // Track the dispatched agent run instance so store mock returns the same ID
        AgentRun? capturedRun = null;

        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<AgentRun>(), It.IsAny<CancellationToken>()))
            .Callback<AgentRun, CancellationToken>((ar, _) =>
            {
                ar.Start(); // Transition to Running
                capturedRun = ar;
            })
            .ReturnsAsync((AgentRun ar, CancellationToken _) =>
            {
                ar.Start();
                return ar;
            });

        // Track quality gates that are saved
        var savedGates = new List<QualityGate>();
        store.Setup(s => s.SaveQualityGateAsync(It.IsAny<QualityGate>(), It.IsAny<CancellationToken>()))
            .Callback<QualityGate, CancellationToken>((g, _) => savedGates.Add(g))
            .Returns(Task.CompletedTask);

        // Return tracked gates when queried
        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => savedGates.ToList());

        // Return captured agent run (Failed on first poll to short-circuit polling loop)
        bool firstPoll = true;
        store.Setup(s => s.GetAgentRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
            {
                if (capturedRun != null && capturedRun.Id == id)
                {
                    if (firstPoll)
                    {
                        firstPoll = false;
                        return capturedRun; // Still Running — not terminal yet
                    }
                    capturedRun.Fail("simulated agent failure");
                    return capturedRun;
                }
                return null;
            });

        // Track saved pipeline runs
        PipelineRun? savedRun = null;
        store.Setup(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRun, CancellationToken>((r, _) => savedRun = r)
            .Returns(Task.CompletedTask);

        store.Setup(s => s.SaveAgentRunAsync(It.IsAny<AgentRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        store.Setup(s => s.SaveCheckpointAsync(It.IsAny<PipelineCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Loop engineer gives up — this should cause pipeline to fail
        loopEngineer.Setup(l => l.AnalyzeAndFixAsync(It.IsAny<LoopContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FixResult
            {
                Outcome = FixOutcome.GiveUp,
                EscalationReason = "Test: cannot fix simulated failure"
            });

        // Real AdaptiveQualityGates with mocked dependencies
        var adaptiveGates = new AdaptiveQualityGates(metricsMock.Object, store.Object);

        // Real AgentPoolManager with mocked dependencies
        var poolManager = new AgentPoolManager(
            dispatcher.Object, store.Object, eventBus.Object, backpressure, costTracker);

        var engine = new PipelineEngine(
            dispatcher.Object,
            store.Object,
            eventBus.Object,
            poolManager,
            backpressure,
            loopEngineer.Object,
            adaptiveGates);

        // Create a simple pipeline run and DAG with one node
        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        var dag = new PipelineDag();
        dag.AddNode("dotnet", PipelinePhase.Implement);

        // Act
        var result = await engine.StartAsync(dag, run);

        // Assert — pipeline should fail because gate evaluation still works

        // 1. Pipeline must end in Failed status (blocked by gates)
        result.Status.Should().Be(PipelineStatus.Failed,
            "gate evaluation should block the pipeline when agents fail");

        // 2. Quality gates must have been created
        savedGates.Should().NotBeEmpty("quality gates should be created during execution");

        // 3. At least one gate must be failed — gate evaluation was not bypassed
        savedGates.Any(g => !g.Passed).Should().BeTrue(
            "failed gates must be recorded; risk metadata must not bypass gate evaluation");

        // 4. Risk metadata must have been stored (advisory — does not affect gate outcomes)
        savedRun?.Metadata.Should().ContainKey("adaptive_risk_checked_at",
            "risk metadata should be stored on the pipeline run");
        savedRun?.Metadata.Should().ContainKey("adaptive_risk_count",
            "risk count should be stored");
    }

    [Fact]
    public async Task StartAsync_SuccessfulAgent_ShouldCreatePassedGatesAndComplete()
    {
        // Arrange
        var pipelineRunId = Guid.NewGuid();
        var dispatcher = new Mock<IAgentDispatcher>();
        var store = new Mock<IStateStore>();
        var eventBus = new Mock<IEventBus>();
        var backpressure = new BackpressureController(100, 100);
        var costTracker = new CostTracker();
        var loopEngineer = new Mock<ILoopEngineer>();
        var metricsMock = new Mock<IAgentMetricsService>();

        SetupAgentProfile(metricsMock, "dotnet", aisScore: 80.0, gatePassRate: 0.95);

        AgentRun? capturedRun = null;

        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<AgentRun>(), It.IsAny<CancellationToken>()))
            .Callback<AgentRun, CancellationToken>((ar, _) =>
            {
                ar.Start();
                capturedRun = ar;
            })
            .ReturnsAsync((AgentRun ar, CancellationToken _) =>
            {
                ar.Start();
                return ar;
            });

        var savedGates = new List<QualityGate>();
        store.Setup(s => s.SaveQualityGateAsync(It.IsAny<QualityGate>(), It.IsAny<CancellationToken>()))
            .Callback<QualityGate, CancellationToken>((g, _) => savedGates.Add(g))
            .Returns(Task.CompletedTask);

        store.Setup(s => s.GetQualityGatesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => savedGates.ToList());

        // Return captured agent run (Completed on first poll)
        store.Setup(s => s.GetAgentRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
            {
                if (capturedRun != null && capturedRun.Id == id)
                {
                    capturedRun.Complete(0.95m, "test-artifact");
                    return capturedRun;
                }
                return null;
            });

        PipelineRun? savedRun = null;
        store.Setup(s => s.SavePipelineRunAsync(It.IsAny<PipelineRun>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRun, CancellationToken>((r, _) => savedRun = r)
            .Returns(Task.CompletedTask);

        store.Setup(s => s.SaveAgentRunAsync(It.IsAny<AgentRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        store.Setup(s => s.SaveCheckpointAsync(It.IsAny<PipelineCheckpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var adaptiveGates = new AdaptiveQualityGates(metricsMock.Object, store.Object);
        var poolManager = new AgentPoolManager(
            dispatcher.Object, store.Object, eventBus.Object, backpressure, costTracker);

        var engine = new PipelineEngine(
            dispatcher.Object,
            store.Object,
            eventBus.Object,
            poolManager,
            backpressure,
            loopEngineer.Object,
            adaptiveGates);

        var run = PipelineRun.Create("test-workflow", new(), "test-trigger");
        var dag = new PipelineDag();
        dag.AddNode("dotnet", PipelinePhase.Implement);

        // Act
        var result = await engine.StartAsync(dag, run);

        // Assert
        result.Status.Should().Be(PipelineStatus.Completed,
            "a successful agent should pass gates and complete");

        savedGates.Should().NotBeEmpty("quality gates should be created");
        savedGates.All(g => g.Passed).Should().BeTrue("all gates should pass for a successful agent");

        savedRun?.Metadata.Should().ContainKey("adaptive_risk_checked_at",
            "risk metadata should still be stored even on success");
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
