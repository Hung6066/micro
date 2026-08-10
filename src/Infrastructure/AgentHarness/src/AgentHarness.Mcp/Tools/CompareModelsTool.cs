using System.Text.Json;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class CompareModelsTool
{
    private readonly EvalEngineService _engine;

    public CompareModelsTool(EvalEngineService engine) => _engine = engine;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var suiteName = parameters.GetValueOrDefault("suite_name")?.ToString()
            ?? throw new ArgumentException("'suite_name' is required.");
        var targetAgent = parameters.GetValueOrDefault("target_agent")?.ToString()
            ?? throw new ArgumentException("'target_agent' is required.");

        var modelsRaw = parameters.GetValueOrDefault("models");
        List<string> models;
        if (modelsRaw is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Array)
        {
            models = jsonEl.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s != "").ToList();
        }
        else if (modelsRaw is string singleModel)
        {
            models = new List<string> { singleModel };
        }
        else
        {
            throw new ArgumentException("'models' must be an array of model names or a single model name.");
        }

        var k = parameters.TryGetValue("k", out var kVal) && int.TryParse(kVal?.ToString(), out var kParsed)
            ? kParsed
            : 5;
        if (k <= 0) throw new ArgumentException("k must be greater than 0.");

        var result = await _engine.CompareModelsAsync(suiteName, targetAgent, models, k);

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
