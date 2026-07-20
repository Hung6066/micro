using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Core.Interfaces;

public interface IStateStore
{
    Task SavePipelineRunAsync(PipelineRun run, CancellationToken ct = default);
    Task<PipelineRun?> GetPipelineRunAsync(Guid id, CancellationToken ct = default);
    Task<List<PipelineRun>> GetChildPipelineRunsAsync(Guid parentPipelineRunId, CancellationToken ct = default);
    Task SaveAgentRunAsync(AgentRun run, CancellationToken ct = default);
    Task<AgentRun?> GetAgentRunAsync(Guid id, CancellationToken ct = default);
    Task SaveQualityGateAsync(QualityGate gate, CancellationToken ct = default);
    Task<List<QualityGate>> GetQualityGatesAsync(Guid pipelineRunId, CancellationToken ct = default);
    Task SaveArtifactAsync(Artifact artifact, CancellationToken ct = default);
    Task<Artifact?> GetArtifactAsync(Guid id, CancellationToken ct = default);
    Task<List<AgentRun>> GetAgentRunsAsync(Guid pipelineRunId, CancellationToken ct = default);
    Task<List<AgentRun>> GetAgentRunsByAgentNameAsync(string agentName, CancellationToken ct = default);
    Task<List<AgentRun>> GetPendingAgentRunsAsync(CancellationToken ct = default);
    Task SaveCheckpointAsync(PipelineCheckpoint checkpoint, CancellationToken ct = default);
    Task<PipelineCheckpoint?> GetLatestCheckpointAsync(Guid pipelineRunId, CancellationToken ct = default);
    Task<List<PipelineRun>> GetRunningPipelinesAsync(CancellationToken ct = default);
    Task SaveMemoryEntryAsync(MemoryEntry entry, CancellationToken ct = default);
    Task<MemoryEntry?> GetMemoryEntryAsync(Guid id, CancellationToken ct = default);
    Task<List<MemoryEntry>> GetMemoryEntriesAsync(CancellationToken ct = default);
    Task SavePendingApprovalAsync(PendingApproval approval, CancellationToken ct = default);
    Task<PendingApproval?> GetPendingApprovalAsync(Guid id, CancellationToken ct = default);
    Task<List<PendingApproval>> GetPendingApprovalsAsync(CancellationToken ct = default);
}
