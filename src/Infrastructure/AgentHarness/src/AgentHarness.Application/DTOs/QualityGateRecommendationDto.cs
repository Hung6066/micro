namespace His.Hope.AgentHarness.Application.DTOs;

/// <summary>
/// Recommended quality gate threshold for a specific agent, based on
/// historical AIS score and gate pass rate. Advisory only — does not
/// modify or bypass existing gate logic.
/// </summary>
public record QualityGateRecommendationDto
{
    /// <summary>The agent this recommendation applies to.</summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// Recommended gate threshold (0.0 to 1.0).
    /// Higher values = stricter gates; lower values = more relaxed gates.
    /// </summary>
    public double RecommendedGateThreshold { get; init; }

    /// <summary>Agent Intelligence Score at the time of recommendation.</summary>
    public double AisScore { get; init; }

    /// <summary>Historical quality gate pass rate for this agent.</summary>
    public double HistoricalPassRate { get; init; }

    /// <summary>When this recommendation was generated.</summary>
    public DateTime LastUpdatedAt { get; init; }
}
