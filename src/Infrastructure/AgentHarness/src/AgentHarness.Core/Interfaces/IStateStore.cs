using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Core.Interfaces;

public interface IStateStore
{
    Task SavePipelineRunAsync(PipelineRun run, CancellationToken ct = default);
    Task<PipelineRun?> GetPipelineRunAsync(Guid id, CancellationToken ct = default);
    Task SaveAgentRunAsync(AgentRun run, CancellationToken ct = default);
    Task<AgentRun?> GetAgentRunAsync(Guid id, CancellationToken ct = default);
    Task SaveQualityGateAsync(QualityGate gate, CancellationToken ct = default);
    Task<List<QualityGate>> GetQualityGatesAsync(Guid pipelineRunId, CancellationToken ct = default);
    Task SaveArtifactAsync(Artifact artifact, CancellationToken ct = default);
    Task<Artifact?> GetArtifactAsync(Guid id, CancellationToken ct = default);
    Task<List<AgentRun>> GetAgentRunsAsync(Guid pipelineRunId, CancellationToken ct = default);
}
