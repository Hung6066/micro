using His.Hope.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Infrastructure.Persistence;

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
            var (filter, error) = await IamTenantAccessGuard.ResolveForMutationAsync(
                db,
                http.User,
                scopeId,
                registry,
                http,
                http.RequestAborted);
            if (error is not null)
                return error;

            IamTenantHttpContext.SetFilter(http, filter);
            return await next(context);
        });
}
