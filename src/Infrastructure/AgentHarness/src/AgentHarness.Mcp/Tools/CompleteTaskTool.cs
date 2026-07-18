using System.Text.Json;
using Serilog;
using His.Hope.AgentHarness.Core.Events;
using His.Hope.AgentHarness.Core.Interfaces;
using His.Hope.AgentHarness.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.AgentHarness.Mcp.Tools;

/// <summary>
/// Marks an agent run as Completed or Failed with result data.
/// Called by external agents (OpenCode) after they finish executing a task.
/// </summary>
public class CompleteTaskTool
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBus _eventBus;

    public CompleteTaskTool(IServiceScopeFactory scopeFactory, IEventBus eventBus)
    {
        _scopeFactory = scopeFactory;
        _eventBus = eventBus;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var agentRunIdStr = parameters.GetValueOrDefault("agent_run_id")?.ToString()
            ?? throw new ArgumentException("'agent_run_id' is required.");
        var statusStr = parameters.GetValueOrDefault("status")?.ToString() ?? "completed";

        if (!Guid.TryParse(agentRunIdStr, out var agentRunId))
            throw new ArgumentException("'agent_run_id' must be a valid GUID.");

        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStateStore>();

        var agentRun = await store.GetAgentRunAsync(agentRunId);
        if (agentRun == null)
            throw new InvalidOperationException($"Agent run {agentRunId} not found.");

        if (agentRun.IsTerminal())
        {
            Log.Warning("Agent run {RunId} is already terminal ({Status}), ignoring complete-task", agentRunId, agentRun.Status);
            return JsonSerializer.Serialize(new
            {
                agent_run_id = agentRunIdStr,
                status = agentRun.Status.ToString(),
                warning = "Already terminal"
            });
        }

        // Parse optional fields
        decimal confidence = 0.95m;
        if (parameters.TryGetValue("confidence", out var confObj) && confObj is JsonElement confEl)
            confidence = confEl.GetDecimal();

        string? artifactRef = null;
        if (parameters.TryGetValue("artifact_ref", out var artObj) && artObj is JsonElement artEl)
            artifactRef = artEl.GetString();

        string? errorMessage = null;
        if (parameters.TryGetValue("error_message", out var errObj) && errObj is JsonElement errEl)
            errorMessage = errEl.GetString();

        switch (statusStr.ToLowerInvariant())
        {
            case "completed":
                agentRun.Complete(confidence, artifactRef ?? $"agents/{agentRun.AgentName}/run-{agentRun.Id:N}");
                await store.SaveAgentRunAsync(agentRun);
                await _eventBus.PublishAsync(
                    new AgentCompleted(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, confidence, artifactRef));
                Log.Information("Agent run {RunId} completed externally: {AgentName} confidence={Confidence}",
                    agentRunId, agentRun.AgentName, confidence);
                break;

            case "failed":
                agentRun.Fail(errorMessage ?? "External agent reported failure");
                await store.SaveAgentRunAsync(agentRun);
                await _eventBus.PublishAsync(
                    new AgentFailed(agentRun.Id, agentRun.PipelineRunId, agentRun.AgentName, errorMessage ?? "Failed", agentRun.RetryCount));
                Log.Warning("Agent run {RunId} failed externally: {AgentName} error={Error}",
                    agentRunId, agentRun.AgentName, errorMessage);
                break;

            default:
                throw new ArgumentException($"Invalid status '{statusStr}'. Use 'completed' or 'failed'.");
        }

        return JsonSerializer.Serialize(new
        {
            agent_run_id = agentRunIdStr,
            status = statusStr
        });
    }
}
