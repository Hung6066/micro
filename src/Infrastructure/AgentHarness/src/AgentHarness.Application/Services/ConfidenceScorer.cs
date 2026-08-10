using His.Hope.AgentHarness.Core.ValueObjects;

namespace His.Hope.AgentHarness.Application.Services;

public enum ChangeSize { Small, Medium, Large }

public class ErrorContext
{
    public bool MatchesKnownPattern { get; set; }
    public ChangeSize ChangeSize { get; set; } = ChangeSize.Small;
    public bool HasSucceededBefore { get; set; }
    public bool IsReversible { get; set; } = true;
    public bool TouchesSecurityBoundary { get; set; }
}

public class ConfidenceScorer
{
    public ConfidenceScore Calculate(ErrorContext ctx)
    {
        var signals = new List<(decimal score, decimal weight)>();

        // Signal 1: Error matches known pattern (weight: 0.4)
        signals.Add((ctx.MatchesKnownPattern ? 1.0m : 0.0m, 0.4m));

        // Signal 2: Fix size (weight: 0.2)
        var sizeScore = ctx.ChangeSize switch
        {
            ChangeSize.Small => 1.0m,
            ChangeSize.Medium => 0.5m,
            _ => 0.0m
        };
        signals.Add((sizeScore, 0.2m));

        // Signal 3: Previous success (weight: 0.2)
        signals.Add((ctx.HasSucceededBefore ? 1.0m : 0.0m, 0.2m));

        // Signal 4: Reversible (weight: 0.1)
        signals.Add((ctx.IsReversible ? 1.0m : 0.0m, 0.1m));

        // Signal 5: No security boundary (weight: 0.1)
        signals.Add((!ctx.TouchesSecurityBoundary ? 1.0m : 0.0m, 0.1m));

        return ConfidenceScore.FromWeightedSignals(signals.ToArray());
    }
}
