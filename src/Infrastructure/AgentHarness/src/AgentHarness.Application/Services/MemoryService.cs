using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Application.Services;

public class MemoryService : IMemoryService
{
    private readonly IStateStore _store;
    private readonly EmbeddingService _embeddings;

    private const double VectorMatchThreshold = 0.6;
    private const double JaccardMatchThreshold = 0.35;

    public MemoryService(IStateStore store, EmbeddingService embeddings)
    {
        _store = store;
        _embeddings = embeddings;
    }

    public async Task<MemoryEntry?> FindSimilarAsync(string errorOutput, string agentName, CancellationToken ct = default)
    {
        var memories = await _store.GetMemoryEntriesAsync(ct);
        if (memories.Count == 0) return null;

        var queryKeywords = MemoryEntry.ExtractKeywords(errorOutput + " " + agentName);
        var queryEmbedding = _embeddings.GenerateEmbedding(errorOutput + " " + agentName);

        MemoryEntry? best = null;
        double bestScore = 0;
        bool usedVector = false;

        foreach (var mem in memories)
        {
            double score;

            if (mem.Embedding is { Length: 256 })
            {
                score = _embeddings.CosineSimilarity(queryEmbedding, mem.Embedding);
                usedVector = true;
            }
            else if (!string.IsNullOrWhiteSpace(queryKeywords) && !string.IsNullOrWhiteSpace(mem.Keywords))
            {
                score = MemoryEntry.ComputeSimilarity(queryKeywords, mem.Keywords);
            }
            else
            {
                continue;
            }

            if (string.Equals(mem.AgentName, agentName, StringComparison.OrdinalIgnoreCase))
                score *= 1.3;

            if (score > bestScore)
            {
                bestScore = score;
                best = mem;
            }
        }

        var threshold = usedVector ? VectorMatchThreshold : JaccardMatchThreshold;
        return bestScore >= threshold ? best : null;
    }

    public async Task StoreAsync(string errorPattern, string errorCategory, string agentName,
        string fixDescription, string? fixArtifactRef, bool success, CancellationToken ct = default)
    {
        var text = errorPattern + " " + errorCategory + " " + agentName;
        var embedding = _embeddings.GenerateEmbedding(text);
        var entry = MemoryEntry.Create(errorPattern, errorCategory, agentName,
            fixDescription, fixArtifactRef, success, embedding);
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
    Task<MemoryEntry?> FindSimilarAsync(string errorOutput, string agentName, CancellationToken ct = default);
    Task StoreAsync(string errorPattern, string errorCategory, string agentName,
        string fixDescription, string? fixArtifactRef, bool success, CancellationToken ct = default);
    Task RecordHitAsync(Guid memoryEntryId, CancellationToken ct = default);
}
