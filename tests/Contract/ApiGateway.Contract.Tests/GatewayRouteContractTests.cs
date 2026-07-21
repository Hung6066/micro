using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace His.Hope.ApiGateway.Contract.Tests;

public class GatewayRouteContractTests
{
    private static readonly string GatewayConfigPath = FindGatewayConfigPath();

    public static TheoryData<string, string> RegressionRoutes => new()
    {
        { "GET", "/api/v1/admin/audit-logs" },
        { "GET", "/api/v1/admin/settings" },
        { "POST", "/api/v1/admin/settings" },
        { "GET", "/api/v1/lab-orders/search" },
        { "POST", "/hubs/lab-critical-alerts/negotiate" },
        { "GET", "/api/v1/patients/9c7d1000-0000-4000-8000-000000000001/encounters" },
        { "GET", "/api/v1/patients/9c7d1000-0000-4000-8000-000000000001/appointments" },
        { "GET", "/api/v1/patients/9c7d1000-0000-4000-8000-000000000001/lab-orders" },
        { "GET", "/api/v1/patients/9c7d1000-0000-4000-8000-000000000001/prescriptions" },
        { "GET", "/api/v1/patients/9c7d1000-0000-4000-8000-000000000001/invoices" },
    };

    [Theory]
    [MemberData(nameof(RegressionRoutes))]
    public void GatewayConfig_ShouldMatchKnownRegressionRoutes_WithoutMethod405(string method, string path)
    {
        var routes = LoadGatewayRoutes();

        var matchingRoutes = routes
            .Where(route => TemplateMatches(route.Path, path))
            .ToList();

        matchingRoutes.Should().NotBeEmpty($"{path} must be routed by the gateway to avoid 404 regressions");
        matchingRoutes.Should().Contain(
            route => route.AcceptsMethod(method),
            $"{path} must accept {method} at the gateway to avoid 405 regressions");
    }

    [Fact]
    public void GatewayConfig_ShouldRoutePatientSubresources_ToOwningServiceClusters()
    {
        var routes = LoadGatewayRoutes();

        routes.Should().ContainEquivalentOf(new GatewayRoute("patient-encounters", "clinical", "/api/v1/patients/{patientId}/encounters", null));
        routes.Should().ContainEquivalentOf(new GatewayRoute("patient-appointments", "appointments", "/api/v1/patients/{patientId}/appointments", null));
        routes.Should().ContainEquivalentOf(new GatewayRoute("patient-lab-orders", "lab", "/api/v1/patients/{patientId}/lab-orders", null));
        routes.Should().ContainEquivalentOf(new GatewayRoute("patient-prescriptions", "pharmacy", "/api/v1/patients/{patientId}/prescriptions", null));
        routes.Should().ContainEquivalentOf(new GatewayRoute("patient-invoices", "billing", "/api/v1/patients/{patientId}/invoices", null));
    }

    private static IReadOnlyList<GatewayRoute> LoadGatewayRoutes()
    {
        using var stream = File.OpenRead(GatewayConfigPath);
        using var document = JsonDocument.Parse(stream);

        var routeEntries = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Routes")
            .EnumerateObject();

        return routeEntries
            .Select(route => new GatewayRoute(
                route.Name,
                route.Value.GetProperty("ClusterId").GetString()!,
                route.Value.GetProperty("Match").GetProperty("Path").GetString()!,
                route.Value.GetProperty("Match").TryGetProperty("Methods", out var methods)
                    ? methods.EnumerateArray().Select(method => method.GetString()!).ToArray()
                    : null))
            .ToList();
    }

    private static bool TemplateMatches(string routeTemplate, string requestPath)
    {
        var routeSegments = SplitPath(routeTemplate);
        var requestSegments = SplitPath(requestPath);

        for (var index = 0; index < routeSegments.Length; index++)
        {
            var routeSegment = routeSegments[index];

            if (routeSegment.StartsWith("{**", StringComparison.Ordinal))
            {
                return true;
            }

            if (index >= requestSegments.Length)
            {
                return false;
            }

            if (routeSegment.StartsWith('{') && routeSegment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(routeSegment, requestSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return routeSegments.Length == requestSegments.Length;
    }

    private static string[] SplitPath(string path) =>
        path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static string FindGatewayConfigPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "ApiGateway", "appsettings.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find src/ApiGateway/appsettings.json from the test output directory.");
    }

    private sealed record GatewayRoute(string RouteId, string ClusterId, string Path, IReadOnlyCollection<string>? Methods)
    {
        public bool AcceptsMethod(string method) =>
            Methods is null || Methods.Contains(method, StringComparer.OrdinalIgnoreCase);
    }
}
