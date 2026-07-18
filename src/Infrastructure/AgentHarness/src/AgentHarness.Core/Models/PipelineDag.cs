namespace His.Hope.AgentHarness.Core.Models;

public enum PipelinePhase { Plan, Implement, Test, Validate, Commit }

public class PipelineDag
{
    public List<PipelineNode> Nodes { get; } = new();
    public List<PipelineEdge> Edges { get; } = new();
    public List<LoopBackEdge> LoopBackEdges { get; } = new();

    public PipelineNode AddNode(string agentName, PipelinePhase phase)
    {
        var node = new PipelineNode
        {
            Id = Guid.NewGuid(),
            AgentName = agentName,
            Phase = phase,
            Status = PipelineStatus.Pending
        };
        Nodes.Add(node);
        return node;
    }

    public PipelineEdge AddEdge(PipelineNode from, PipelineNode to, string? condition = null)
    {
        var edge = new PipelineEdge { From = from, To = to, Condition = condition };
        Edges.Add(edge);
        return edge;
    }

    public LoopBackEdge AddLoopBackEdge(PipelineNode from, PipelineNode to, int maxIterations, string viaAgent)
    {
        var edge = new LoopBackEdge
        {
            From = from,
            To = to,
            MaxIterations = maxIterations,
            ViaAgent = viaAgent
        };
        LoopBackEdges.Add(edge);
        return edge;
    }

    public IEnumerable<PipelineNode> GetPhaseNodes(PipelinePhase phase) => Nodes.Where(n => n.Phase == phase);
    public IEnumerable<PipelineNode> GetGateNodes() => Nodes.Where(n => n.IsGate);
}

public class PipelineNode
{
    public Guid Id { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public PipelinePhase Phase { get; set; }
    public PipelineStatus Status { get; set; }
    public bool IsGate { get; set; }
    public string? GateId { get; set; }
    public string? Condition { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PipelineEdge
{
    public required PipelineNode From { get; set; }
    public required PipelineNode To { get; set; }
    public string? Condition { get; set; }
}

public class LoopBackEdge
{
    public required PipelineNode From { get; set; }
    public required PipelineNode To { get; set; }
    public int MaxIterations { get; set; }
    public string ViaAgent { get; set; } = "loop-engineer";
    public int CurrentIteration { get; set; }
}
