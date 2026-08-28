using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace His.Hope.AspNetCore.Tenancy;

public static class HisHopeTenantScopeMiddlewareExtensions
{
    public static IApplicationBuilder UseHisHopeTenantScope(this IApplicationBuilder app) =>
        app.UseMiddleware<HisHopeTenantScopeMiddleware>();
}

internal sealed class HisHopeTenantScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantKey = context.ResolveActiveTenant();
        using (HisHopeTenantScope.Begin(tenantKey))
            await next(context);
    }
}
