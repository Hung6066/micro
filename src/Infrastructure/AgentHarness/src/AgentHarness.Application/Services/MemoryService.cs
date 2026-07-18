using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

/// <summary>
/// Memory service for the Loop Engineer.
/// Stores past error patterns + fixes, queries by similarity.
/// No external embedding API — uses normalized keyword Jaccard similarity.
/// </summary>
public class MemoryService : IMemoryService
{
    private readonly IStateStore _store;

    // Minimum similarity threshold to consider a match
    private const double MatchThreshold = 0.35;

    public MemoryService(IStateStore store)
    {
        _store = store;
    }

    public async Task<MemoryEntry?> FindSimilarAsync(string errorOutput, string agentName, CancellationToken ct = default)
    {
        var memories = await _store.GetMemoryEntriesAsync(ct);
        if (memories.Count == 0) return null;

        var queryKeywords = MemoryEntry.ExtractKeywords(errorOutput + " " + agentName);
        if (string.IsNullOrWhiteSpace(queryKeywords)) return null;

        MemoryEntry? best = null;
        double bestScore = 0;

        foreach (var mem in memories)
        {
            // Prefer memories for the same agent
            var score = MemoryEntry.ComputeSimilarity(queryKeywords, mem.Keywords);

            // Boost by agent match
            if (string.Equals(mem.AgentName, agentName, StringComparison.OrdinalIgnoreCase))
                score *= 1.3;

            if (score > bestScore)
            {
                bestScore = score;
                best = mem;
            }
        }

        return bestScore >= MatchThreshold ? best : null;
    }

    public async Task StoreAsync(string errorPattern, string errorCategory, string agentName,
        string fixDescription, string? fixArtifactRef, bool success, CancellationToken ct = default)
    {
        var entry = MemoryEntry.Create(errorPattern, errorCategory, agentName,
            fixDescription, fixArtifactRef, success);
        await _store.SaveMemoryEntryAsync(entry, ct);
    }

    public async Task RecordHitAsync(Guid memoryEntryId, CancellationToken ct = default)
    {
        var entry = await _store.GetMemoryEntryAsync(memoryEntryId, ct);
        if (entry != null)
        {
            entry.RecordHit();
            await _store.SaveMemoryEntryAsync(entry, ct);
        }
    }
}

public interface IMemoryService
{
    /// <summary>Find a past memory similar to the given error output.</summary>
    Task<MemoryEntry?> FindSimilarAsync(string errorOutput, string agentName, CancellationToken ct = default);

    /// <summary>Store a new memory entry after a fix attempt.</summary>
    Task StoreAsync(string errorPattern, string errorCategory, string agentName,
        string fixDescription, string? fixArtifactRef, bool success, CancellationToken ct = default);

    /// <summary>Record that a memory was used again (increment hit count).</summary>
    Task RecordHitAsync(Guid memoryEntryId, CancellationToken ct = default);
}
