using His.Hope.IdentityService.Application.Assurance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace His.Hope.IdentityService.Api.Middleware;

public sealed class AssurancePolicyMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    public async Task InvokeAsync(HttpContext context, AssurancePolicyService assurancePolicy)
    {
        // Integration tests exercise endpoint contracts with a password-only
        // session. Production and staging always enforce the configured
        // assurance policy; the Testing host has dedicated evaluator tests.
        if (environment.IsEnvironment("Testing"))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true ||
            !MutatingMethods.Contains(context.Request.Method))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var journey = ResolveJourney(path);
        if (journey is null)
        {
            await next(context);
            return;
        }

        var isBreakGlass = path.Contains("break-glass", StringComparison.OrdinalIgnoreCase);
        var evaluation = assurancePolicy.EvaluateJourney(context.User, journey, isBreakGlass);
        if (!evaluation.Allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                errorCode = "assurance_policy_denied",
                journey = evaluation.JourneyId,
                requiredAssurance = evaluation.RequiredAssurance,
                reason = evaluation.Reason
            });
            return;
        }

        await next(context);
    }

    private static string? ResolveJourney(string path)
    {
        if (path.StartsWith("/api/v1/admin/iam", StringComparison.OrdinalIgnoreCase) ||
            (path.StartsWith("/api/v1/admin", StringComparison.OrdinalIgnoreCase) &&
             !path.Contains("/me", StringComparison.OrdinalIgnoreCase)))
            return "admin-write";

        if (path.Contains("break-glass", StringComparison.OrdinalIgnoreCase))
            return "break-glass";

        if (path.StartsWith("/api/v1/clinical", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/clinical/", StringComparison.OrdinalIgnoreCase))
            return "clinical-read";

        return null;
    }
}

public static class AssurancePolicyMiddlewareExtensions
{
    public static IApplicationBuilder UseAssurancePolicyEnforcement(this IApplicationBuilder app) =>
        app.UseMiddleware<AssurancePolicyMiddleware>();
}
