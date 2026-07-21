using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Aggregators;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/metrics")]
[Authorize]
public sealed class MetricsController : ControllerBase
{
    private readonly IMetricsAggregator _metricsAggregator;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(IMetricsAggregator metricsAggregator, ILogger<MetricsController> logger)
    {
        _metricsAggregator = metricsAggregator;
        _logger = logger;
    }

    [HttpGet("{service}")]
    public async Task<IActionResult> GetMetrics(
        string service,
        [FromQuery] string[]? metrics = null,
        [FromQuery] string range = "1h",
        CancellationToken ct = default)
    {
        metrics ??= ["cpu", "memory"];
        _logger.LogInformation(
            "Getting metrics: service={Service}, metrics={Metrics}, range={Range}",
            service, metrics, range);

        var result = await _metricsAggregator.GetMetricsAsync(service, metrics, range, ct);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _metricsAggregator.GetSummaryAsync(ct);
        return Ok(summary);
    }
}
