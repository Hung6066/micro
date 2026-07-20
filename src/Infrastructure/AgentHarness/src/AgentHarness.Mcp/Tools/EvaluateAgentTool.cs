using System.Text.Json;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class EvaluateAgentTool
{
    private readonly EvalEngineService _engine;

    public EvaluateAgentTool(EvalEngineService engine) => _engine = engine;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var suiteName = parameters.GetValueOrDefault("suite_name")?.ToString()
            ?? throw new ArgumentException("'suite_name' is required.");
        var targetAgent = parameters.GetValueOrDefault("target_agent")?.ToString()
            ?? throw new ArgumentException("'target_agent' is required.");
        var targetModel = parameters.GetValueOrDefault("target_model")?.ToString();
        var k = parameters.TryGetValue("k", out var kVal) && int.TryParse(kVal?.ToString(), out var kParsed)
            ? kParsed
            : 5;

        var result = await _engine.RunSuiteAsync(suiteName, targetAgent, targetModel, k);

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
