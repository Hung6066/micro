using System.Text.Json;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class GetAgentProfileTool
{
    private readonly AgentMetricsService _service;

    public GetAgentProfileTool(AgentMetricsService service) => _service = service;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString()
            ?? throw new ArgumentException("'agent_name' is required.");

        var profile = await _service.GetAgentProfileAsync(agentName);

        // Return the DTO shape directly using default (camelCase) serialization
        return JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
