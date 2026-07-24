using His.Hope.Identity.Grpc;
using His.Hope.Infrastructure.Security.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.IdentityService.Api.Authorization;

/// <summary>
/// Authorization handler that checks permissions via gRPC call to IdentityService.
/// Falls back to local JWT claim check if gRPC is unavailable (circuit open).
/// </summary>
public class GrpcPermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly His.Hope.Identity.Grpc.IdentityService.IdentityServiceClient _identityClient;
    private readonly ILogger<GrpcPermissionHandler> _logger;

    public GrpcPermissionHandler(
        His.Hope.Identity.Grpc.IdentityService.IdentityServiceClient identityClient,
        ILogger<GrpcPermissionHandler> logger)
    {
        _identityClient = identityClient;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Permission denied: user not authenticated");
            return;
        }

        var userId = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Permission denied: no user ID in claims");
            return;
        }

        try
        {
            var response = await _identityClient.CheckPermissionAsync(
                new CheckPermissionRequest
                {
                    UserId = userId,
                    PermissionCode = requirement.PermissionCode
                });

            if (response.HasPermission)
            {
                _logger.LogDebug("Permission granted via gRPC: {Permission}", requirement.PermissionCode);
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("Permission denied via gRPC: {Permission}", requirement.PermissionCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "gRPC permission check failed, falling back to local claim");

            // Fallback: check JWT claims locally
            var permissionsClaims = context.User.FindAll("permissions")
                .SelectMany(c => c.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (permissionsClaims.Contains(requirement.PermissionCode))
            {
                context.Succeed(requirement);
            }
        }
    }
}
