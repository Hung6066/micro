using System.Text.Json;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class QueryInstinctsTool
{
    private readonly IStateStore _store;

    public QueryInstinctsTool(IStateStore store) => _store = store;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var errorPattern = parameters.GetValueOrDefault("error_pattern")?.ToString()
            ?? throw new ArgumentException("'error_pattern' is required.");
        var agentNameFilter = parameters.GetValueOrDefault("agent_name")?.ToString();
        var minConfidence = parameters.GetValueOrDefault("min_confidence") as double? ?? 0.3;

        var entries = await _store.GetMemoryEntriesAsync(CancellationToken.None);
        if (entries.Count == 0)
        {
            return JsonSerializer.Serialize(new { results = Array.Empty<object>() });
        }

        var queryKeywords = MemoryEntry.ExtractKeywords(errorPattern);
        if (string.IsNullOrWhiteSpace(queryKeywords))
        {
            return JsonSerializer.Serialize(new { results = Array.Empty<object>() });
        }

        var scored = new List<object>();

        foreach (var entry in entries)
        {
            // Apply agent name filter
            if (!string.IsNullOrWhiteSpace(agentNameFilter) &&
                !string.Equals(entry.AgentName, agentNameFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var entryKeywords = MemoryEntry.ExtractKeywords(
                entry.ErrorPattern + " " + entry.ErrorCategory + " " + entry.AgentName);

            if (string.IsNullOrWhiteSpace(entryKeywords))
                continue;

            var similarity = MemoryEntry.ComputeSimilarity(queryKeywords, entryKeywords);

            if (similarity >= minConfidence)
            {
                scored.Add(new
                {
                    instinct_id = entry.Id.ToString(),
                    agent_name = entry.AgentName,
                    error_pattern = entry.ErrorPattern,
                    error_category = entry.ErrorCategory,
                    fix_description = entry.FixDescription,
                    fix_artifact_ref = entry.FixArtifactRef,
                    confidence = Math.Round(similarity, 2),
                    use_count = entry.UseCount,
                    created_at = entry.CreatedAt.ToString("o"),
                    last_used_at = entry.LastUsedAt.ToString("o")
                });
            }
        }

        // Sort by confidence descending, then by use count descending
        var sorted = scored
            .OrderByDescending(r => (double)r.GetType().GetProperty("confidence")?.GetValue(r)!)
            .ThenByDescending(r => (int)r.GetType().GetProperty("use_count")?.GetValue(r)!)
            .ToList();

        return JsonSerializer.Serialize(new { results = sorted });
    }
}
