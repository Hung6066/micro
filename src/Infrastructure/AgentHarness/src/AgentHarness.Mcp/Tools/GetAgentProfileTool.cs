using System.Text.Json;
using His.Hope.AgentHarness.Application.DTOs;
using His.Hope.AgentHarness.Application.Services;
using His.Hope.AgentHarness.Core.Interfaces;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class GetAgentProfileTool
{
    private readonly AgentMetricsService _service;
    private readonly IAgentMetricsRecorder _recorder;

    public GetAgentProfileTool(AgentMetricsService service, IAgentMetricsRecorder recorder)
    {
        _service = service;
        _recorder = recorder;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var agentName = parameters.GetValueOrDefault("agent_name")?.ToString()
            ?? throw new ArgumentException("'agent_name' is required.");

        _recorder.RecordProfileQuery();
        var profile = await _service.GetAgentProfileAsync(agentName);
        _recorder.RecordAisScore(profile.AisScore);

        return JsonSerializer.Serialize(profile, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
