using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.Authorization;

/// <summary>
/// In canary mode, deny when OpenFGA explicitly rejects a locally-allowed decision.
/// Shadow mode remains telemetry-only via <see cref="LoggingAuthorizationShadowProbe"/>.
/// </summary>
public sealed class OpenFgaCanaryAuthorizer(
    IOpenFgaClient openFga,
    ILogger<OpenFgaCanaryAuthorizer> logger,
    IConfiguration? configuration = null)
{
    private readonly string _mode = (configuration?["AUTHZ_PDP_MODE"] ?? "disabled").Trim().ToLowerInvariant();

    public async Task<bool> AllowsAsync(ClaimsPrincipal principal, string permissionCode, CancellationToken cancellationToken = default)
    {
        if (_mode != "canary")
            return true;

        var subject = principal.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject))
        {
            logger.LogWarning("OpenFGA canary denied permission {Permission}: subject is missing", permissionCode);
            return false;
        }

        var relation = permissionCode.Replace(':', '_').Replace('.', '_').Replace('/', '_');
        var external = await openFga.CheckAsync($"user:{subject}", relation, $"permission:{permissionCode}", cancellationToken);
        if (external is null)
        {
            logger.LogWarning("OpenFGA canary denied permission {Permission}: PDP unavailable", permissionCode);
            return false;
        }

        if (external.Value)
            return true;

        logger.LogWarning(
            "OpenFGA canary denied permission {Permission} for subject {Subject}",
            permissionCode,
            subject);
        return false;
    }
}
