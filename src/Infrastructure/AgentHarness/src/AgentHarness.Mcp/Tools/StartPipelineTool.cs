using System.Text.Json;
using MediatR;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class StartPipelineTool
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly IMediator _mediator;

    public StartPipelineTool(IMediator mediator) => _mediator = mediator;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var workflowId = parameters.GetValueOrDefault("workflow_id")?.ToString()
            ?? throw new ArgumentException("'workflow_id' is required.");
        var triggeredBy = parameters.GetValueOrDefault("triggered_by")?.ToString() ?? "system";

        var paramsDict = new Dictionary<string, string>();

        // "params" may arrive as Dictionary<string, object> or JsonElement
        if (parameters.TryGetValue("params", out var paramsObj) && paramsObj != null)
        {
            Dictionary<string, object>? rawParams = null;
            if (paramsObj is Dictionary<string, object> dictParams)
            {
                rawParams = dictParams;
            }
            else if (paramsObj is JsonElement jsonEl && jsonEl.ValueKind == JsonValueKind.Object)
            {
                rawParams = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonEl.GetRawText(), JsonOpts);
            }

            if (rawParams != null)
            {
                foreach (var kvp in rawParams)
                    paramsDict[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
        }

        var command = new StartPipelineCommand(workflowId, paramsDict, triggeredBy);
        var run = await _mediator.Send(command);

        return JsonSerializer.Serialize(new
        {
            pipeline_run_id = run.Id.ToString(),
            status = run.Status.ToString(),
            workflow_id = run.WorkflowId
        });
    }
}
