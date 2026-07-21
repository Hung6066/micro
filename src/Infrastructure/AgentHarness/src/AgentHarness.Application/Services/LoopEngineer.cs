using His.Hope.AgentHarness.Core.ValueObjects;
using Serilog;

namespace His.Hope.AgentHarness.Application.Services;

public class LoopEngineer : ILoopEngineer
{
    private readonly ErrorClassifier _classifier;
    private readonly ConfidenceScorer _scorer;
    private readonly IMemoryService _memory;
    private readonly LlmJudgeService _judge;
    private readonly int _maxIterations;

    public LoopEngineer(
        ErrorClassifier classifier,
        ConfidenceScorer scorer,
        IMemoryService memory,
        LlmJudgeService judge,
        int maxIterations = 3)
    {
        _classifier = classifier;
        _scorer = scorer;
        _memory = memory;
        _judge = judge;
        _maxIterations = maxIterations;
    }

    public async Task<FixResult> AnalyzeAndFixAsync(LoopContext context, CancellationToken ct)
    {
        if (context.PreviousIteration >= _maxIterations)
        {
            return new FixResult
            {
                Outcome = FixOutcome.GiveUp,
                EscalationReason = $"Max iterations ({_maxIterations}) reached",
                ConfidenceScore = 0m
            };
        }

        var result = new FixResult();
        bool allAutoFixed = true;
        bool anyFixed = false;

        foreach (var gate in context.FailedGates)
        {
            var output = gate.Output ?? string.Empty;
            var gateType = gate.GateType ?? gate.GateName ?? "unknown";

            // Step 1: Check memory for known fix
            var memoryEntry = await _memory.FindSimilarAsync(output, gateType, ct);
            if (memoryEntry != null)
            {
                await _memory.RecordHitAsync(memoryEntry.Id, ct);
                result.Changes.Add($"Known fix from memory (use #{memoryEntry.UseCount}): {memoryEntry.FixDescription}");
                anyFixed = true;
                result.ConfidenceScore = Math.Max(result.ConfidenceScore, 0.85m);
                result.MemoryHit = true;
                Log.Information("LoopEngineer found memory match: {Fix} (confidence={Conf})",
                    memoryEntry.FixDescription, result.ConfidenceScore);
                continue;
            }

            // Step 2: Classify error
            var category = _classifier.Classify(output);

            if (!_classifier.IsAutoFixable(category))
            {
                result.Outcome = FixOutcome.Escalated;
                result.EscalationReason = $"Unfixable error category: {category}";
                result.ConfidenceScore = 0.1m;
                return result;
            }

            var fences = CheckSafetyFences(gate);
            if (fences.Any())
            {
                result.Outcome = FixOutcome.Escalated;
                result.EscalationReason = $"Safety fence violations: {string.Join(", ", fences)}";
                result.ConfidenceScore = 0m;
                return result;
            }

            var errorCtx = new ErrorContext
            {
                MatchesKnownPattern = category != ErrorCategory.Unknown,
                ChangeSize = EstimateChangeSize(output),
                HasSucceededBefore = memoryEntry != null,
                IsReversible = category != ErrorCategory.QualityGateFailure
            };

            var confidence = _scorer.Calculate(errorCtx);

            if (confidence.IsAutoFixable)
            {
                result.Changes.Add($"Auto-fix for gate '{gate.GateName}': {category}");
                anyFixed = true;
                result.ConfidenceScore = Math.Max(result.ConfidenceScore, confidence.Value);

                // Store the fix attempt in memory
                await _memory.StoreAsync(
                    output,
                    category.ToString(),
                    gate.GateType ?? "unknown",
                    $"Auto-fix applied: {category} for {gate.GateName}",
                    null,
                    true, ct);
            }
            else
            {
                allAutoFixed = false;
                result.UnfixedIssues.Add(gate.GateName ?? gate.GateId ?? "unknown-gate");
            }
        }

        if (!anyFixed)
        {
            result.Outcome = FixOutcome.Escalated;
            result.EscalationReason = $"Confidence too low for any auto-fix. Unfixed gates: {string.Join(", ", result.UnfixedIssues)}";
        }
        else if (allAutoFixed)
        {
            result.Outcome = FixOutcome.AutoFixed;
            result.JudgeScore = _judge.EvaluateQuality(
                string.Join("\n", context.FailedGates.Select(g => g.Output ?? string.Empty)),
                "loop-engineer");
            result.ConfidenceScore = Math.Max(result.ConfidenceScore, result.JudgeScore.NumericScore / 100m);
        }
        else
            result.Outcome = FixOutcome.PartialFix;

        return result;
    }

    public Task<bool> EvaluateLoopContinuationAsync(PipelineRun run, LoopBackEdge loopEdge, CancellationToken ct = default)
    {
        var canContinue = run.Status == PipelineStatus.Running &&
                          loopEdge.CurrentIteration < loopEdge.MaxIterations;
        return Task.FromResult(canContinue);
    }

    public Task<AgentRun> ExecuteLoopIterationAsync(PipelineRun run, LoopBackEdge loopEdge, CancellationToken ct = default)
    {
        loopEdge.CurrentIteration++;
        var agentRun = AgentRun.Create(
            run.Id,
            loopEdge.ViaAgent,
            $"Loop iteration {loopEdge.CurrentIteration}/{loopEdge.MaxIterations}: review failed gate '{loopEdge.From.GateId ?? loopEdge.From.AgentName}' and route back to '{loopEdge.To.AgentName}'.",
            maxRetries: 1,
            timeoutSeconds: 600);
        return Task.FromResult(agentRun);
    }

    private List<string> CheckSafetyFences(QualityGate gate)
    {
        var violations = new List<string>();
        var restrictedPaths = new[] { "vault/", "/secrets/", "/certificates/", "opencode.json" };
        foreach (var path in restrictedPaths)
        {
            if ((gate.Output ?? string.Empty).Contains(path, StringComparison.OrdinalIgnoreCase))
                violations.Add($"Touches restricted path: {path}");
        }
        return violations;
    }

    private ChangeSize EstimateChangeSize(string output)
    {
        if (output.Length < 200) return ChangeSize.Small;
        if (output.Length < 1000) return ChangeSize.Medium;
        return ChangeSize.Large;
    }

    private bool CheckPreviousSuccess(string gateId) => false; // Future: knowledge base integration
}
