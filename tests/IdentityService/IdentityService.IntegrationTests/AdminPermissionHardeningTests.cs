using His.Hope.SharedKernel.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class AdminPermissionHardeningTests(IdentityServiceTestFixture fixture)
{
    [Fact]
    public void Admin_permission_policy_constants_cover_new_manage_permissions()
    {
        Assert.Equal(
            $"Permission:{HisHopePermissions.Admin.ProvisioningManage}",
            AuthorizationPolicyNames.Permissions.AdminProvisioningManage);
        Assert.Equal(
            $"Permission:{HisHopePermissions.Admin.SecuritySignalsManage}",
            AuthorizationPolicyNames.Permissions.AdminSecuritySignalsManage);
    }

    [Fact]
    public void High_risk_admin_endpoints_require_elevated_policies()
    {
        using var scope = fixture.Services.CreateScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();
        var endpoints = dataSource.Endpoints.OfType<RouteEndpoint>().ToList();

        AssertEndpointPolicy(
            endpoints,
            "POST",
            "/api/v1/admin/ldap/sync",
            AuthorizationPolicyNames.Permissions.AdminUsersWrite);
        AssertEndpointPolicy(
            endpoints,
            "POST",
            "/api/v1/admin/security/rotate-signing-key",
            AuthorizationPolicyNames.Permissions.AdminSettingsWrite);
        AssertEndpointPolicy(
            endpoints,
            "POST",
            "/api/v1/admin/provisioning/queue",
            AuthorizationPolicyNames.Permissions.AdminProvisioningManage);
        AssertEndpointPolicy(
            endpoints,
            "POST",
            "/api/v1/admin/security-signals/outbox/{id:guid}/retry",
            AuthorizationPolicyNames.Permissions.AdminSecuritySignalsManage);
    }

    private static void AssertEndpointPolicy(
        IReadOnlyCollection<RouteEndpoint> endpoints,
        string method,
        string routePattern,
        string expectedPolicy)
    {
        var endpoint = endpoints.SingleOrDefault(item =>
            string.Equals(item.RoutePattern.RawText, routePattern, StringComparison.OrdinalIgnoreCase) &&
            item.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        Assert.NotNull(endpoint);
        var policies = endpoint!.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(item => item.Policy)
            .Where(policy => !string.IsNullOrWhiteSpace(policy))
            .ToArray();
        Assert.Contains(expectedPolicy, policies);
    }
}
