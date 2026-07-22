using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Authorization;
using SystemDashboard.Bff.Services;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize(Roles = DashboardRoles.ReadOnly)]
public sealed class AlertsController : ControllerBase
{
    private readonly IAlertManagerService _alertService;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(IAlertManagerService alertService, ILogger<AlertsController> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlerts(CancellationToken ct)
    {
        _logger.LogInformation("Fetching alerts from AlertManager");
        var alerts = await _alertService.GetAlertsAsync(ct);
        return Ok(alerts);
    }
}
