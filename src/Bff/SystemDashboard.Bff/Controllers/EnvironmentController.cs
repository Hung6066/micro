using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemDashboard.Bff.Authorization;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/environment")]
[Authorize(Roles = DashboardRoles.ReadOnly)]
public sealed class EnvironmentController : ControllerBase
{
    [HttpGet]
    public IActionResult GetEnvironment()
    {
        var env = new EnvironmentContext
        {
            Name = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };
        return Ok(env);
    }
}
