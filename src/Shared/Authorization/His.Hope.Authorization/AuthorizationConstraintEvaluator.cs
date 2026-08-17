using System.Security.Claims;
using System.Text.Json;

namespace His.Hope.Authorization;

/// <summary>
/// Evaluates the bounded resource attributes carried by an issued principal.
/// Constraints are emitted by Identity Service from an approved permission
/// boundary; resource services only provide trusted resource metadata.
/// </summary>
internal static class AuthorizationConstraintEvaluator
{
    public static string? Evaluate(ClaimsPrincipal principal, AuthorizationResource resource)
    {
        foreach (var claim in principal.FindAll("authorization_constraints"))
        {
            try
            {
                using var document = JsonDocument.Parse(claim.Value);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return "resource_constraint_invalid";

                var root = document.RootElement;
                if (root.TryGetProperty("tenant", out var tenant) && tenant.ValueKind == JsonValueKind.String)
                {
                    var expectedTenant = tenant.GetString();
                    var actualTenant = principal.FindFirst("tenant_id")?.Value
                        ?? principal.FindFirst("tenant")?.Value
                        ?? resource.TenantId;
                    if (string.IsNullOrWhiteSpace(expectedTenant) ||
                        !string.Equals(expectedTenant, actualTenant, StringComparison.OrdinalIgnoreCase))
                        return "resource_constraint_denied";
                }

                if (root.TryGetProperty("facility", out var facility) && facility.ValueKind == JsonValueKind.String &&
                    !string.Equals(facility.GetString(), resource.FacilityId, StringComparison.OrdinalIgnoreCase))
                    return "resource_constraint_denied";

                if (root.TryGetProperty("facilities", out var facilities) && facilities.ValueKind == JsonValueKind.Array)
                {
                    var allowed = facilities.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (allowed.Count == 0 || resource.FacilityId is null || !allowed.Contains(resource.FacilityId))
                        return "resource_constraint_denied";
                }

                if (root.TryGetProperty("resourceTypes", out var types) && types.ValueKind == JsonValueKind.Array)
                {
                    var allowed = types.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (allowed.Count == 0 || !allowed.Contains(resource.Type))
                        return "resource_constraint_denied";
                }
            }
            catch (JsonException)
            {
                return "resource_constraint_invalid";
            }
        }

        return null;
    }
}
