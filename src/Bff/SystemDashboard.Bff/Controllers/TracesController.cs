using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Aggregators;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/traces")]
[Authorize]
public sealed class TracesController : ControllerBase
{
    private readonly ITracesAggregator _tracesAggregator;
    private readonly ILogger<TracesController> _logger;

    public TracesController(ITracesAggregator tracesAggregator, ILogger<TracesController> logger)
    {
        _tracesAggregator = tracesAggregator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> SearchTraces(
        [FromQuery] string? service,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] long? minDurationMs,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Searching traces: service={Service}, from={From}, to={To}, minDurationMs={MinDurationMs}, limit={Limit}",
            service, from, to, minDurationMs, limit);

        var traces = await _tracesAggregator.SearchTracesAsync(service ?? "", from, to, minDurationMs, limit, ct);
        return Ok(traces);
    }

    [HttpGet("{traceId}")]
    public async Task<IActionResult> GetTrace(string traceId, CancellationToken ct)
    {
        var trace = await _tracesAggregator.GetTraceAsync(traceId, ct);
        if (trace is null)
            return NotFound();
        return Ok(trace);
    }
}
