namespace His.Hope.AgentHarness.Core.Interfaces;

public class FixResult
{
    public FixOutcome Outcome { get; set; }
    public string? EscalationReason { get; set; }
    public decimal ConfidenceScore { get; set; }
    public List<string> Changes { get; set; } = new();
    public List<string> UnfixedIssues { get; set; } = new();
    public bool MemoryHit { get; set; }
}
