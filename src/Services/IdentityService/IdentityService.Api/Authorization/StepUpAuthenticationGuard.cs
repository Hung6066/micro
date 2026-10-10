using System.Security.Claims;
using His.Hope.SharedKernel.Protocol;
using His.Hope.IdentityService.Application.Assurance;
using Microsoft.AspNetCore.Http;

namespace His.Hope.IdentityService.Api.Authorization;

/// <summary>
/// Shared guard for operations that change access or cross a tenant boundary.
/// A role alone is not sufficient: the current session must carry an MFA-capable
/// authentication method and a recent authentication timestamp.
/// </summary>
public static class StepUpAuthenticationGuard
{
    public const int DefaultMaxAgeMinutes = 15;

    public static bool HasMfa(ClaimsPrincipal principal) => principal.FindAll(HisHopeProtocolConstants.Claims.AuthenticationMethod)
        .Any(claim => claim.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase) ||
                      claim.Value.Equals("totp", StringComparison.OrdinalIgnoreCase) ||
                      claim.Value.Equals("passkey", StringComparison.OrdinalIgnoreCase) ||
                      claim.Value.Equals("webauthn", StringComparison.OrdinalIgnoreCase));

    public static bool HasFreshMfa(
        ClaimsPrincipal principal,
        int maxAgeMinutes = DefaultMaxAgeMinutes) =>
        HasMfa(principal) && AssuranceClaimResolver.HasFreshAuthentication(principal, maxAgeMinutes);

    public static IResult? RequireFreshMfa(
        HttpContext http,
        int maxAgeMinutes = DefaultMaxAgeMinutes) =>
        HasFreshMfa(http.User, maxAgeMinutes)
            ? null
            : Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Step-up authentication required",
                detail: $"A fresh MFA authentication within {maxAgeMinutes} minutes is required.",
                extensions: new Dictionary<string, object?> { ["errorCode"] = "step_up_required" });

    /// <summary>
    /// Protects administrative mutation groups while leaving read endpoints
    /// available without repeating the step-up challenge.
    /// </summary>
    public static async ValueTask<object?> RequireFreshMfaForMutationFilter(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var isMutation = HttpMethods.IsPost(context.HttpContext.Request.Method) ||
                         HttpMethods.IsPut(context.HttpContext.Request.Method) ||
                         HttpMethods.IsPatch(context.HttpContext.Request.Method) ||
                         HttpMethods.IsDelete(context.HttpContext.Request.Method);
        var isReadOnlyPost = HttpMethods.IsPost(context.HttpContext.Request.Method) &&
                             IsReadOnlyPost(context.HttpContext.Request.Path);

        if (isMutation && !isReadOnlyPost)
        {
            if (RequireFreshMfa(context.HttpContext) is { } failure)
                return failure;
        }

        return await next(context);
    }

    private static bool IsReadOnlyPost(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.EndsWith("/policies/lint", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("/policies/compile", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("/analyzer", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith("/analyzer/new-access-diff", StringComparison.OrdinalIgnoreCase);
    }
}
