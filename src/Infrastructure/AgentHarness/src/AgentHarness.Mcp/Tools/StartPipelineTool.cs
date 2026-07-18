using System.Text.Json;
using MediatR;
using His.Hope.AgentHarness.Application.Commands.StartPipeline;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class StartPipelineTool
{
    private readonly IMediator _mediator;

    public StartPipelineTool(IMediator mediator) => _mediator = mediator;

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var workflowId = parameters.GetValueOrDefault("workflow_id")?.ToString()
            ?? throw new ArgumentException("'workflow_id' is required.");
        var triggeredBy = parameters.GetValueOrDefault("triggered_by")?.ToString() ?? "system";

        var paramsDict = new Dictionary<string, string>();
        if (parameters.TryGetValue("params", out var paramsObj) && paramsObj is Dictionary<string, object> rawParams)
        {
            foreach (var kvp in rawParams)
                paramsDict[kvp.Key] = kvp.Value?.ToString() ?? "";
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
