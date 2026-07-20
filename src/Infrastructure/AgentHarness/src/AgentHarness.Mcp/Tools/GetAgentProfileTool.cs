using System.Text.Json;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Interfaces;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class GetAgentProfileTool
{
    private readonly IAgentMetricsService _service;

    public GetAgentProfileTool(IAgentMetricsService service) => _service = service;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString()
            ?? throw new ArgumentException("'agent_name' is required.");

        var profile = await _service.GetAgentProfileAsync(agentName);

        return JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
