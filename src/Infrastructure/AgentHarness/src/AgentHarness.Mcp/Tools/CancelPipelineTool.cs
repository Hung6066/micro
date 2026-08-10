using System.Text.Json;
using MediatR;
using Serilog;
using His.Hope.AgentHarness.Application.Commands.CancelPipeline;
using His.Hope.AgentHarness.Application.Services;

namespace His.Hope.AgentHarness.Mcp.Tools;

public class CancelPipelineTool
{
    private readonly IMediator _mediator;
    private readonly GuardrailService _guardrails;

    public CancelPipelineTool(IMediator mediator, GuardrailService guardrails)
    {
        _mediator = mediator;
        _guardrails = guardrails;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var pipelineRunIdStr = parameters.GetValueOrDefault("pipeline_run_id")?.ToString()
            ?? throw new ArgumentException("'pipeline_run_id' is required.");
        var reason = parameters.GetValueOrDefault("reason")?.ToString() ?? "Cancelled by user";
        var requestedBy = parameters.GetValueOrDefault("requested_by")?.ToString() ?? "system";

        if (!Guid.TryParse(pipelineRunIdStr, out var pipelineRunId))
            throw new ArgumentException("'pipeline_run_id' must be a valid GUID.");

        // Guardrail check
        var guardrail = _guardrails.Validate("cancel-pipeline", requestedBy, reason);
        if (guardrail.IsBlocked)
            return JsonSerializer.Serialize(new { success = false, blocked = true, message = guardrail.Reason });
        if (guardrail.NeedsApproval)
            return JsonSerializer.Serialize(new { success = false, needs_approval = true, message = guardrail.Reason });

        var command = new CancelPipelineCommand(pipelineRunId, reason);
        var success = await _mediator.Send(command);

        Log.Information("Pipeline cancelled: {PipelineId} by {RequestedBy}", pipelineRunIdStr, requestedBy);

        return JsonSerializer.Serialize(new
        {
            success,
            message = success
                ? $"Pipeline '{pipelineRunIdStr}' cancelled."
                : $"Failed to cancel pipeline '{pipelineRunIdStr}'."
        });
    }
}
