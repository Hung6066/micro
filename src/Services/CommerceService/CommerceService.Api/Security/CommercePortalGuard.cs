using System.Security.Claims;

namespace His.Hope.CommerceService.Api.Security;

internal static class CommercePortalGuard
{
    public const string PortalClassEndUser = "end_user";
    public const string PortalClassOperator = "operator";
    public const string PortalClassCustomerOperator = "customer_operator";

    public static string? GetPortalClass(ClaimsPrincipal user) =>
        user.FindFirst("portal_class")?.Value;

    public static string? GetClientId(ClaimsPrincipal user) =>
        user.FindFirst("client_id")?.Value
        ?? user.FindFirst("azp")?.Value;

    /// <summary>Fail closed when portal_class is missing on commerce routes.</summary>
    public static bool HasRequiredPortalClass(ClaimsPrincipal user) =>
        !string.IsNullOrWhiteSpace(GetPortalClass(user));

    public static bool IsEndUser(ClaimsPrincipal user) =>
        string.Equals(GetPortalClass(user), PortalClassEndUser, StringComparison.OrdinalIgnoreCase);

    public static bool IsOperator(ClaimsPrincipal user) =>
        string.Equals(GetPortalClass(user), PortalClassOperator, StringComparison.OrdinalIgnoreCase);

    public static bool IsOperatorOnlyPath(PathString path, string method) =>
        path.Value?.Contains("/status", StringComparison.OrdinalIgnoreCase) == true
        && HttpMethods.IsPatch(method);
}
