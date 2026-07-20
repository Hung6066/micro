using System.Text.Json;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class RecordInstinctTool
{
    private readonly IMemoryService _memory;

    public RecordInstinctTool(IMemoryService memory) => _memory = memory;

    private static double ExtractDouble(Dictionary<string, object> dict, string key, double defaultValue)
    {
        if (dict.TryGetValue(key, out var val))
        {
            if (val is double d) return d;
            if (val is JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                return je.GetDouble();
        }
        return defaultValue;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString()
            ?? throw new ArgumentException("'agent_name' is required.");
        var errorPattern = parameters.GetValueOrDefault("error_pattern")?.ToString()
            ?? throw new ArgumentException("'error_pattern' is required.");
        var errorCategory = parameters.GetValueOrDefault("error_category")?.ToString()
            ?? throw new ArgumentException("'error_category' is required.");
        var fixDescription = parameters.GetValueOrDefault("fix_description")?.ToString()
            ?? throw new ArgumentException("'fix_description' is required.");
        var fixArtifactRef = parameters.GetValueOrDefault("fix_artifact_ref")?.ToString();
        var confidence = ExtractDouble(parameters, "confidence", 0.85);

        await _memory.StoreAsync(errorPattern, errorCategory, agentName,
            fixDescription, fixArtifactRef, success: true, CancellationToken.None);

        return JsonSerializer.Serialize(new
        {
            status = "recorded",
            agent = agentName,
            category = errorCategory,
            confidence
        });
    }
}
