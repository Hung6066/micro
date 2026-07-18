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

    /// <summary>Reads agent run via a FRESH DbContext to bypass EF Core change tracker cache.
    /// Required by PipelineEngine polling — ensures we see external updates from complete-task.</summary>
    public async Task<AgentRun?> GetAgentRunAsync(Guid id, CancellationToken ct = default)
        => await _db.AgentRuns.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

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

    public async Task<List<AgentRun>> GetPendingAgentRunsAsync(CancellationToken ct = default)
        => await _db.AgentRuns
            .Where(a => a.Status == AgentRunStatus.Running)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task SaveCheckpointAsync(PipelineCheckpoint checkpoint, CancellationToken ct = default)
    {
        _db.Set<PipelineCheckpoint>().Add(checkpoint);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PipelineCheckpoint?> GetLatestCheckpointAsync(Guid pipelineRunId, CancellationToken ct = default)
        => await _db.Set<PipelineCheckpoint>()
            .Where(c => c.PipelineRunId == pipelineRunId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<List<PipelineRun>> GetRunningPipelinesAsync(CancellationToken ct = default)
        => await _db.PipelineRuns
            .Where(p => p.Status == PipelineStatus.Running)
            .ToListAsync(ct);

    public async Task SaveMemoryEntryAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        var existing = await _db.Set<MemoryEntry>().FindAsync([entry.Id], ct);
        if (existing is null)
            _db.Set<MemoryEntry>().Add(entry);
        else
            _db.Entry(existing).CurrentValues.SetValues(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MemoryEntry?> GetMemoryEntryAsync(Guid id, CancellationToken ct = default)
        => await _db.Set<MemoryEntry>().FindAsync([id], ct);

    public async Task<List<MemoryEntry>> GetMemoryEntriesAsync(CancellationToken ct = default)
        => await _db.Set<MemoryEntry>()
            .OrderByDescending(m => m.UseCount)
            .ThenByDescending(m => m.LastUsedAt)
            .ToListAsync(ct);
}
