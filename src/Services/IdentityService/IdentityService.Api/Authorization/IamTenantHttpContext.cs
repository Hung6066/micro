using Microsoft.AspNetCore.Http;

namespace His.Hope.IdentityService.Api.Authorization;

public static class IamTenantHttpContext
{
    public const string FilterItemKey = "__IamTenantScopeFilter";

    public static void SetFilter(HttpContext http, IamTenantScopeFilter filter) =>
        http.Items[FilterItemKey] = filter;

    public static IamTenantScopeFilter? GetFilter(HttpContext http) =>
        http.Items.TryGetValue(FilterItemKey, out var value)
            ? value as IamTenantScopeFilter
            : null;

    public static IamTenantScopeFilter RequireFilter(HttpContext http) =>
        GetFilter(http)
        ?? throw new InvalidOperationException(
            "Tenant scope was not resolved. Apply WithTenantReadScope or WithTenantMutationScope.");

    public static Guid? ParseScopeId(HttpRequest request)
    {
        if (!request.Query.TryGetValue("scopeId", out var values))
            return null;

        var raw = values.FirstOrDefault();
        return Guid.TryParse(raw, out var scopeId) ? scopeId : null;
    }
}
