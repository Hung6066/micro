namespace His.Hope.AgentHarness.Core.Models;

public enum PipelinePhase { Plan, Implement, Test, Validate, Commit }

/// <summary>Defines when a node should execute relative to its dependencies.</summary>
public enum BranchCondition { Always, OnSuccess, OnFailure, OnTimeout, Never }

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

    public PipelineEdge AddEdge(PipelineNode from, PipelineNode to, BranchCondition? condition = null)
    {
        var edge = new PipelineEdge { From = from, To = to, Condition = condition ?? BranchCondition.Always };
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

    /// <summary>
    /// Gets nodes for a phase, respecting branch conditions.
    /// Skips nodes whose conditions aren't met based on dependency statuses.
    /// </summary>
    public IEnumerable<PipelineNode> GetPhaseNodes(PipelinePhase phase)
    {
        var phaseNodes = Nodes.Where(n => n.Phase == phase).ToList();
        if (!Edges.Any()) return phaseNodes; // No edges = simple phase routing

        return phaseNodes.Where(node => ShouldExecute(node));
    }

    private bool ShouldExecute(PipelineNode node)
    {
        // Find all edges pointing TO this node
        var incomingEdges = Edges.Where(e => e.To.Id == node.Id).ToList();
        if (!incomingEdges.Any()) return true; // No dependencies = always run

        // Check each edge condition against the source node status
        return incomingEdges.All(edge =>
        {
            var sourceStatus = edge.From.Status;
            return edge.Condition switch
            {
                BranchCondition.Always => true,
                BranchCondition.OnSuccess => sourceStatus == PipelineStatus.Completed,
                BranchCondition.OnFailure => sourceStatus == PipelineStatus.Failed,
                BranchCondition.OnTimeout => sourceStatus == PipelineStatus.Failed, // Treat as failure
                BranchCondition.Never => false,
                _ => true
            };
        });
    }

    public IEnumerable<PipelineNode> GetGateNodes() => Nodes.Where(n => n.IsGate);
}

public class PipelineNode
{
    public Guid Id { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string TaskDescription { get; set; } = string.Empty;
    public PipelinePhase Phase { get; set; }
    public PipelineStatus Status { get; set; }
    public bool IsGate { get; set; }
    public string? GateId { get; set; }
    public BranchCondition Condition { get; set; } = BranchCondition.Always;
    public int AttemptNumber { get; set; } = 1;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PipelineEdge
{
    public required PipelineNode From { get; set; }
    public required PipelineNode To { get; set; }
    public BranchCondition Condition { get; set; } = BranchCondition.Always;
}

public class LoopBackEdge
{
    public required PipelineNode From { get; set; }
    public required PipelineNode To { get; set; }
    public int MaxIterations { get; set; }
    public string ViaAgent { get; set; } = "loop-engineer";
    public int CurrentIteration { get; set; }
}
