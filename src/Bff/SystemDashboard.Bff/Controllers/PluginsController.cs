using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using His.Hope.Configuration;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Controllers;

[ApiController]
[Route("api/plugins")]
[Authorize(Policy = "Permission:dashboard.view")]
public sealed class PluginsController(
    IServicePluginRegistry plugins,
    IAuthorizationService authorization) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServicePluginDto>>> GetPlugins()
    {
        var result = new List<ServicePluginDto>();
        foreach (var plugin in plugins.Enabled.Where(plugin =>
                     !string.IsNullOrWhiteSpace(plugin.DashboardRoute)))
        {
            var allowed = plugin.Permissions.Length == 0;
            foreach (var permission in plugin.Permissions)
            {
                var decision = await authorization.AuthorizeAsync(
                    User,
                    $"Permission:{permission}");
                if (decision.Succeeded)
                {
                    allowed = true;
                    break;
                }
            }

            if (allowed)
                result.Add(new ServicePluginDto(
                    plugin.Key,
                    plugin.DisplayName,
                    plugin.DashboardRoute,
                    plugin.Icon,
                    plugin.Permissions));
        }

        return Ok(result.ToArray());
    }
}
