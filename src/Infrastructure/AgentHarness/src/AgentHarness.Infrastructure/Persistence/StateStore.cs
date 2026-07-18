using Microsoft.EntityFrameworkCore;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Infrastructure.Persistence;

public class StateStore : IStateStore
{
    private readonly HarnessDbContext _db;

    public StateStore(HarnessDbContext db) => _db = db;

    public async Task SavePipelineRunAsync(PipelineRun run, CancellationToken ct = default)
    {
        var existing = await _db.PipelineRuns.FindAsync([run.Id], ct);
        if (existing is null)
            _db.PipelineRuns.Add(run);
        else
            _db.Entry(existing).CurrentValues.SetValues(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PipelineRun?> GetPipelineRunAsync(Guid id, CancellationToken ct = default)
        => await _db.PipelineRuns.FindAsync([id], ct);

    public async Task SaveAgentRunAsync(AgentRun run, CancellationToken ct = default)
    {
        var existing = await _db.AgentRuns.FindAsync([run.Id], ct);
        if (existing is null)
            _db.AgentRuns.Add(run);
        else
            _db.Entry(existing).CurrentValues.SetValues(run);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AgentRun?> GetAgentRunAsync(Guid id, CancellationToken ct = default)
        => await _db.AgentRuns.FindAsync([id], ct);

    public async Task SaveQualityGateAsync(QualityGate gate, CancellationToken ct = default)
    {
        var existing = await _db.QualityGates.FindAsync([gate.Id], ct);
        if (existing is null)
            _db.QualityGates.Add(gate);
        else
            _db.Entry(existing).CurrentValues.SetValues(gate);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<QualityGate>> GetQualityGatesAsync(Guid pipelineRunId, CancellationToken ct = default)
        => await _db.QualityGates
            .Where(g => g.PipelineRunId == pipelineRunId)
            .ToListAsync(ct);

    public async Task SaveArtifactAsync(Artifact artifact, CancellationToken ct = default)
    {
        var existing = await _db.Artifacts.FindAsync([artifact.Id], ct);
        if (existing is null)
            _db.Artifacts.Add(artifact);
        else
            _db.Entry(existing).CurrentValues.SetValues(artifact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<Artifact?> GetArtifactAsync(Guid id, CancellationToken ct = default)
        => await _db.Artifacts.FindAsync([id], ct);

    public async Task<List<AgentRun>> GetAgentRunsAsync(Guid pipelineRunId, CancellationToken ct = default)
        => await _db.AgentRuns
            .Where(a => a.PipelineRunId == pipelineRunId)
            .ToListAsync(ct);
}
