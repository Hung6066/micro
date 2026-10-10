using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.Authorization;

/// <summary>
/// P2 shadow seam. A probe may compare the local P1 decision with an external
/// PDP, but it cannot alter the returned decision or grant access.
/// </summary>
public interface IAuthorizationShadowProbe
{
    ValueTask ObserveAsync(
        AuthorizationContext context,
        AuthorizationDecision localDecision,
        CancellationToken cancellationToken = default);
}

public sealed class NullAuthorizationShadowProbe : IAuthorizationShadowProbe
{
    public ValueTask ObserveAsync(AuthorizationContext context, AuthorizationDecision localDecision,
        CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

public sealed class LoggingAuthorizationShadowProbe(
    IConfiguration configuration,
    IOpenFgaClient openFga,
    ILogger<LoggingAuthorizationShadowProbe> logger) : IAuthorizationShadowProbe
{
    private readonly string _mode = (configuration["AUTHZ_PDP_MODE"] ?? "disabled").Trim().ToLowerInvariant();

    public async ValueTask ObserveAsync(AuthorizationContext context, AuthorizationDecision localDecision,
        CancellationToken cancellationToken = default)
    {
        if (_mode is not ("shadow" or "canary"))
            return;

        var subject = context.Principal.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject)?.Value ?? context.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var resource = context.Resource;
        bool? external = null;
        if (!string.IsNullOrWhiteSpace(subject) && resource is not null)
        {
            var relation = Normalize(context.Action);
            external = await openFga.CheckAsync($"user:{subject}", relation, $"{resource.Type}:{resource.CanonicalId}", cancellationToken);
        }
        logger.LogInformation(
            "Authorization shadow decision: mode={Mode} localStatus={Status} externalStatus={ExternalStatus} action={Action} resourceType={ResourceType}",
            _mode, localDecision.Status, external is null ? "unavailable" : external.Value ? "allow" : "deny", localDecision.Action, localDecision.ResourceType);
    }

    private static string Normalize(string action) => action.Replace(':', '_').Replace('.', '_').Replace('/', '_');
}
