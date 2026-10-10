using His.Hope.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;

namespace His.Hope.IdentityService.Api.Authorization;

public static class IamTenantEndpointExtensions
{
    public static RouteHandlerBuilder WithTenantReadScope(
        this RouteHandlerBuilder builder,
        string permissionAction) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var db = http.RequestServices.GetRequiredService<IdentityDbContext>();
            var crossTenantPolicy = http.RequestServices.GetRequiredService<ICrossTenantAccessPolicy>();
            var registry = http.RequestServices.GetService<IConglomerateTenantRegistry>();
            var scopeId = IamTenantHttpContext.ParseScopeId(http.Request);
            var (filter, error) = await IamTenantAccessGuard.ResolveForReadAsync(
                db,
                http.User,
                scopeId,
                permissionAction,
                crossTenantPolicy,
                registry,
                http.RequestAborted);
            if (error is not null)
                return error;

            IamTenantHttpContext.SetFilter(http, filter);
            return await next(context);
        });

    public static RouteHandlerBuilder WithTenantMutationScope(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var db = http.RequestServices.GetRequiredService<IdentityDbContext>();
            var registry = http.RequestServices.GetService<IConglomerateTenantRegistry>();
            var scopeId = IamTenantHttpContext.ParseScopeId(http.Request);
            var permissionAction = ResolveMutationPermission(http);
            var (filter, error) = await IamTenantAccessGuard.ResolveForMutationAsync(
                db,
                http.User,
                scopeId,
                registry,
                http,
                http.RequestAborted,
                permissionAction);
            if (error is not null)
                return error;

            IamTenantHttpContext.SetFilter(http, filter);
            return await next(context);
        });

    private static string? ResolveMutationPermission(HttpContext http) =>
        http.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(metadata => metadata.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy) &&
                policy.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
            .Select(policy => policy!["Permission:".Length..])
            .FirstOrDefault(permission => !string.IsNullOrWhiteSpace(permission));
}
