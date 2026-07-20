namespace His.Hope.AgentHarness.Application.DTOs;

/// <summary>
/// Advisory failure risk prediction for an agent/phase node.
/// This is informational only — it never bypasses or auto-passes quality gates.
/// </summary>
public record FailureRiskDto
{
    /// <summary>Predicted failure probability between 0.0 (low risk) and 1.0 (high risk).</summary>
    public double RiskScore { get; init; }

    /// <summary>Categorical risk level: Low, Medium, High, or Critical.</summary>
    public string RiskLevel { get; init; } = "Medium";

    /// <summary>Human-readable explanation of the risk prediction.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Suggested model for re-evaluation, if applicable.</summary>
    public string? SuggestedModel { get; init; }
}
