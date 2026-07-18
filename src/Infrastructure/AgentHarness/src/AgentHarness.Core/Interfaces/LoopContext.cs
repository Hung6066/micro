using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Core.Interfaces;

public class LoopContext
{
    public List<QualityGate> FailedGates { get; set; } = new();
    public int PreviousIteration { get; set; }
}
