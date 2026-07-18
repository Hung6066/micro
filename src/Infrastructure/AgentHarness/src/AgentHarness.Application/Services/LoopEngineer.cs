using His.Hope.AgentHarness.Core.ValueObjects;

namespace His.Hope.AgentHarness.Application.Services;

public class LoopEngineer : ILoopEngineer
{
    private readonly ErrorClassifier _classifier;
    private readonly ConfidenceScorer _scorer;
    private const int MaxIterations = 3;

    public LoopEngineer(ErrorClassifier classifier, ConfidenceScorer scorer)
    {
        _classifier = classifier;
        _scorer = scorer;
    }

    public async Task<FixResult> AnalyzeAndFixAsync(LoopContext context, CancellationToken ct)
    {
        if (context.PreviousIteration >= MaxIterations)
        {
            return new FixResult
            {
                Outcome = FixOutcome.GiveUp,
                EscalationReason = $"Max iterations ({MaxIterations}) reached",
                ConfidenceScore = 0m
            };
        }

        var result = new FixResult();
        bool allAutoFixed = true;
        bool anyFixed = false;

        foreach (var gate in context.FailedGates)
        {
            var category = _classifier.Classify(gate.Output ?? string.Empty);

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
                ChangeSize = EstimateChangeSize(gate.Output ?? string.Empty),
                HasSucceededBefore = CheckPreviousSuccess(gate.GateId),
                IsReversible = category != ErrorCategory.QualityGateFailure
            };

            var confidence = _scorer.Calculate(errorCtx);

            if (confidence.IsAutoFixable)
            {
                result.Changes.Add($"Auto-fix for gate '{gate.GateName}': {category}");
                anyFixed = true;
                result.ConfidenceScore = Math.Max(result.ConfidenceScore, confidence.Value);
            }
            else
            {
                allAutoFixed = false;
                result.UnfixedIssues.Add(gate.GateName);
            }
        }

        if (!anyFixed)
        {
            result.Outcome = FixOutcome.Escalated;
            result.EscalationReason = $"Confidence too low for any auto-fix. Unfixed gates: {string.Join(", ", result.UnfixedIssues)}";
        }
        else if (allAutoFixed)
            result.Outcome = FixOutcome.AutoFixed;
        else
            result.Outcome = FixOutcome.PartialFix;

        return result;
    }

    public Task<bool> EvaluateLoopContinuationAsync(PipelineRun run, LoopBackEdge loopEdge, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<AgentRun> ExecuteLoopIterationAsync(PipelineRun run, LoopBackEdge loopEdge, CancellationToken ct = default)
    {
        throw new NotImplementedException();
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
