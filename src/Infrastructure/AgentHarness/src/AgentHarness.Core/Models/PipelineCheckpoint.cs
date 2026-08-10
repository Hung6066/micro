using System.Text.Json;

namespace His.Hope.AgentHarness.Core.Models;

/// <summary>
/// Snapshot of pipeline execution state at a point in time.
/// Enables crash recovery: if the container restarts, the pipeline
/// can resume from the last checkpoint instead of starting over.
/// </summary>
public class PipelineCheckpoint
{
    public Guid Id { get; private set; }
    public Guid PipelineRunId { get; private set; }
    public string Phase { get; private set; } = string.Empty;
    public string CompletedNodeIds { get; private set; } = "[]";
    public string FailedNodeIds { get; private set; } = "[]";
    public string NodeStatesJson { get; private set; } = "{}";
    public int LoopIteration { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PipelineCheckpoint() { }

    public static PipelineCheckpoint Create(
        Guid pipelineRunId,
        string phase,
        List<PipelineNode> completedNodes,
        List<PipelineNode> failedNodes,
        int loopIteration)
    {
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Use stable keys: "agent|phase|task" — survives container restart (GUIDs change)
        string NodeKey(PipelineNode n) => $"{n.AgentName}|{n.Phase}|{n.TaskDescription}";

        return new PipelineCheckpoint
        {
            Id = Guid.NewGuid(),
            PipelineRunId = pipelineRunId,
            Phase = phase,
            CompletedNodeIds = JsonSerializer.Serialize(completedNodes.Select(NodeKey).ToList(), opts),
            FailedNodeIds = JsonSerializer.Serialize(failedNodes.Select(NodeKey).ToList(), opts),
            NodeStatesJson = JsonSerializer.Serialize(completedNodes.ToDictionary(NodeKey, n => n), opts),
            LoopIteration = loopIteration,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Completed node keys: "agent|phase|task" — stable across restarts (unlike GUIDs).
    /// </summary>
    public List<string> GetCompletedNodeIds()
    {
        try { return JsonSerializer.Deserialize<List<string>>(CompletedNodeIds) ?? []; }
        catch { return []; }
    }

    public List<string> GetFailedNodeIds()
    {
        try { return JsonSerializer.Deserialize<List<string>>(FailedNodeIds) ?? []; }
        catch { return []; }
    }
}
