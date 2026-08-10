namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Implements light-weight multi-agent consensus for high-risk changes.
/// When activated, runs a primary agent, reviews its output with the Loop Engineer,
/// and if confidence is insufficient, spawns a secondary agent with an alternative
/// approach and selects the higher-confidence result.
/// </summary>
public class ConsensusOrchestrator
{
    private readonly IAgentDispatcher _dispatcher;
    private readonly ILoopEngineer _loopEngineer;

    public ConsensusOrchestrator(IAgentDispatcher dispatcher, ILoopEngineer loopEngineer)
    {
        _dispatcher = dispatcher;
        _loopEngineer = loopEngineer;
    }

    /// <summary>
    /// Determines whether consensus mode should be activated based on the change scope.
    /// Returns <c>true</c> for security changes, proto changes, or migration changes.
    /// </summary>
    public bool ShouldUseConsensus(ChangeScope scope)
    {
        return scope.HasSecurityChanges ||
               scope.HasProtoChanges ||
               scope.HasMigrationChanges;
    }

    /// <summary>
    /// Runs consensus by dispatching a primary agent, reviewing with the Loop Engineer,
    /// and optionally spawning a secondary agent if the review outcome needs it.
    /// Returns the <see cref="AgentRun"/> with the highest confidence.
    /// </summary>
    public async Task<AgentRun> RunConsensusAsync(
        Guid pipelineRunId,
        string taskDescription,
        CancellationToken ct)
    {
        // Primary agent
        var primary = AgentRun.Create(pipelineRunId, "dotnet", taskDescription);
        primary = await _dispatcher.DispatchAsync(primary, ct);

        // Loop Engineer review
        var context = new LoopContext { PreviousIteration = 0 };
        var review = await _loopEngineer.AnalyzeAndFixAsync(context, ct);

        if (review.Outcome == FixOutcome.AutoFixed)
            return primary; // Consensus reached

        // Spawn secondary agent with different approach
        var secondaryTask = $"Implement with alternative approach: {taskDescription}";
        var secondary = AgentRun.Create(pipelineRunId, "dotnet", secondaryTask);
        secondary = await _dispatcher.DispatchAsync(secondary, ct);

        // Return the one with higher confidence
        return (primary.ConfidenceScore ?? 0) >= (secondary.ConfidenceScore ?? 0)
            ? primary
            : secondary;
    }
}
