using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Authorization;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize(Roles = DashboardRoles.ReadOnly)]
public sealed class LogsController : ControllerBase
{
    private readonly ILogsAggregator _logsAggregator;
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogsAggregator logsAggregator, ILogger<LogsController> logger)
    {
        _logsAggregator = logsAggregator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> QueryLogs(
        [FromQuery] string? service,
        [FromQuery] string? level,
        [FromQuery] int? from,
        [FromQuery] int size = 100,
        [FromQuery] string? query = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Querying logs: service={Service}, level={Level}, from={From}, size={Size}, query={Query}",
            service, level, from, size, query);

        var logs = await _logsAggregator.QueryLogsAsync(service, level, from, size, query, ct);
        _logger.LogInformation("Returning {Count} log entries", logs.Count);
        return Ok(logs);
    }
}
