using System.Text.Json;
using System.Text.Json.Nodes;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class QueryInstinctsTool
{
    private readonly IStateStore _store;

    public QueryInstinctsTool(IStateStore store) => _store = store;

    private static double ExtractDouble(Dictionary<string, object> dict, string key, double defaultValue)
    {
        if (dict.TryGetValue(key, out var val))
        {
            if (val is double d) return d;
            if (val is JsonElement je && je.ValueKind == JsonValueKind.Number)
                return je.GetDouble();
        }
        return defaultValue;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var errorPattern = parameters.GetValueOrDefault("error_pattern")?.ToString()
            ?? throw new ArgumentException("'error_pattern' is required.");
        var agentNameFilter = parameters.GetValueOrDefault("agent_name")?.ToString();
        var minConfidence = ExtractDouble(parameters, "min_confidence", 0.3);

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

        var scored = new List<ScoredInstinct>();

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
                scored.Add(new ScoredInstinct
                {
                    InstinctId = entry.Id.ToString(),
                    AgentName = entry.AgentName,
                    ErrorPattern = entry.ErrorPattern,
                    ErrorCategory = entry.ErrorCategory,
                    FixDescription = entry.FixDescription,
                    FixArtifactRef = entry.FixArtifactRef,
                    Confidence = Math.Round(similarity, 2),
                    UseCount = entry.UseCount,
                    CreatedAt = entry.CreatedAt.ToString("o"),
                    LastUsedAt = entry.LastUsedAt.ToString("o")
                });
            }
        }

        // Sort by confidence descending, then by use count descending
        var sorted = scored
            .OrderByDescending(r => r.Confidence)
            .ThenByDescending(r => r.UseCount)
            .ToList();

        return JsonSerializer.Serialize(new { results = sorted });
    }

    private sealed class ScoredInstinct
    {
        [System.Text.Json.Serialization.JsonPropertyName("instinct_id")]
        public string InstinctId { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("agent_name")]
        public string AgentName { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("error_pattern")]
        public string ErrorPattern { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("error_category")]
        public string ErrorCategory { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("fix_description")]
        public string FixDescription { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("fix_artifact_ref")]
        public string? FixArtifactRef { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public double Confidence { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("use_count")]
        public int UseCount { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("last_used_at")]
        public string LastUsedAt { get; set; } = "";
    }
}
