using FluentAssertions;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.UnitTests.Services;

public class LoopEngineerTests
{
    [Fact]
    public async Task AnalyzeAndFix_WhenConfidenceBelowThreshold_ShouldEscalate()
    {
        var engine = new LoopEngineer(new ErrorClassifier(), new ConfidenceScorer());
        var context = new LoopContext
        {
            FailedGates = new List<QualityGate>
            {
                QualityGate.Create(Guid.NewGuid(), Guid.NewGuid(), "test-gate", "Test Gate", GateSeverity.Block)
            },
            PreviousIteration = 1
        };
        context.FailedGates[0].MarkFailed("Unknown semantic error in business logic");
        var result = await engine.AnalyzeAndFixAsync(context, CancellationToken.None);
        result.Outcome.Should().Be(FixOutcome.Escalated);
        result.EscalationReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeAndFix_WhenKnownPattern_ShouldAutoFix()
    {
        var engine = new LoopEngineer(new ErrorClassifier(), new ConfidenceScorer());
        var context = new LoopContext
        {
            FailedGates = new List<QualityGate>
            {
                QualityGate.Create(Guid.NewGuid(), Guid.NewGuid(), "build-integrity", "Build Integrity", GateSeverity.Block)
            },
            PreviousIteration = 0
        };
        context.FailedGates[0].MarkFailed("error CS0246: The type or namespace name 'UserMfa' could not be found");
        var result = await engine.AnalyzeAndFixAsync(context, CancellationToken.None);
        result.Outcome.Should().Be(FixOutcome.AutoFixed);
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.8m);
    }

    [Fact]
    public async Task AnalyzeAndFix_MaxIterations_ShouldGiveUp()
    {
        var engine = new LoopEngineer(new ErrorClassifier(), new ConfidenceScorer());
        var context = new LoopContext { FailedGates = new(), PreviousIteration = 3 };
        var result = await engine.AnalyzeAndFixAsync(context, CancellationToken.None);
        result.Outcome.Should().Be(FixOutcome.GiveUp);
    }
}
