using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Aggregators;
using SystemDashboard.Bff.Authorization;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/resources")]
public sealed class ResourcesController : ControllerBase
{
    private readonly IResourceAggregator _resourceAggregator;
    private readonly ILifecycleController _lifecycleController;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(
        IResourceAggregator resourceAggregator,
        ILifecycleController lifecycleController,
        ILogger<ResourcesController> logger)
    {
        _resourceAggregator = resourceAggregator;
        _lifecycleController = lifecycleController;
        _logger = logger;
    }

    [Authorize(Roles = DashboardRoles.ReadOnly)]
    [HttpGet]
    public async Task<IActionResult> GetAllResources(CancellationToken ct)
    {
        var resources = await _resourceAggregator.GetAllResourcesAsync(ct);
        return Ok(resources);
    }

    [Authorize(Roles = DashboardRoles.ReadOnly)]
    [HttpGet("{name}")]
    public async Task<IActionResult> GetResourceByName(string name, CancellationToken ct)
    {
        var resource = await _resourceAggregator.GetResourceByNameAsync(name, ct);
        if (resource is null)
            return NotFound();
        return Ok(resource);
    }

    [Authorize(Roles = DashboardRoles.Manage)]
    [HttpPost("{name}/start")]
    public async Task<IActionResult> StartService(string name, CancellationToken ct)
    {
        var success = await _lifecycleController.StartAsync(name, ct);
        if (!success)
            return StatusCode(500, new { error = $"Failed to start service '{name}'" });
        _logger.LogInformation("Service {Name} started successfully", name);
        return Ok(new { message = $"Service '{name}' started" });
    }

    [Authorize(Roles = DashboardRoles.Manage)]
    [HttpPost("{name}/stop")]
    public async Task<IActionResult> StopService(string name, CancellationToken ct)
    {
        var success = await _lifecycleController.StopAsync(name, ct);
        if (!success)
            return StatusCode(500, new { error = $"Failed to stop service '{name}'" });
        _logger.LogInformation("Service {Name} stopped successfully", name);
        return Ok(new { message = $"Service '{name}' stopped" });
    }

    [Authorize(Roles = DashboardRoles.Manage)]
    [HttpPost("{name}/restart")]
    public async Task<IActionResult> RestartService(string name, CancellationToken ct)
    {
        var success = await _lifecycleController.RestartAsync(name, ct);
        if (!success)
            return StatusCode(500, new { error = $"Failed to restart service '{name}'" });
        _logger.LogInformation("Service {Name} restarted successfully", name);
        return Ok(new { message = $"Service '{name}' restarted" });
    }
}
