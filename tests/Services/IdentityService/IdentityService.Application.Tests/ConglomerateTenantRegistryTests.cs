using His.Hope.IdentityService.Application.Conglomerate;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace His.Hope.IdentityService.Application.Tests;

public sealed class ConglomerateTenantRegistryTests
{
    [Fact]
    public void Disabled_registry_is_safe_default_for_every_lookup()
    {
        var registry = new DisabledConglomerateTenantRegistry();

        Assert.False(registry.IsEnabled);
        Assert.Equal(ConglomerateConstants.HqCustomerVisibilityNone, registry.HqCustomerVisibility);
        Assert.False(registry.IsConglomerateClient(null));
        Assert.False(registry.IsConglomerateClient("client"));
        Assert.Null(registry.GetClientTenant("client"));
        Assert.Equal(ConglomerateConstants.PortalClassOperator, registry.GetPortalClass("client"));
        Assert.Equal(ConglomerateConstants.TenantClassInternal, registry.GetTenantClass("tenant"));
        Assert.Null(registry.GetOperatorHome("tenant"));
        Assert.Empty(registry.GetClientIdsForTenant("tenant"));
        Assert.Empty(registry.GetCustomerTenantsForOperator("operator"));
        Assert.False(registry.IsCustomerTenant("tenant"));
        Assert.Empty(registry.AllowedCrossTenantPairs);
        Assert.Null(registry.GetTenantProfile("tenant"));
    }

    [Fact]
    public void Registry_resolves_profiles_clients_and_policy_from_options()
    {
        var options = new ConglomerateOptions
        {
            Enabled = true,
            HqCustomerVisibility = ConglomerateConstants.HqCustomerVisibilityAll,
            Tenants =
            [
                new() { Key = "group-hq", TenantClass = ConglomerateConstants.TenantClassInternal },
                new() { Key = "customer-a", TenantClass = ConglomerateConstants.TenantClassCustomer, OperatorHome = "GROUP-HQ" },
                new() { Key = "", TenantClass = ConglomerateConstants.TenantClassCustomer }
            ],
            OidcClientTenants = new(StringComparer.Ordinal) { ["portal-a"] = "customer-a" },
            OidcClientPortalClasses = new(StringComparer.OrdinalIgnoreCase) { ["portal-a"] = ConglomerateConstants.PortalClassCustomerOperator },
            CrossTenantPolicy = new()
            {
                DefaultDeny = false,
                AllowedPairs = [new() { Source = "group-hq", Target = "customer-a", Permissions = ["admin.audit.read"] }]
            }
        };

        var registry = CreateRegistry(options);

        Assert.True(registry.IsEnabled);
        Assert.Equal(ConglomerateConstants.HqCustomerVisibilityAll, registry.HqCustomerVisibility);
        Assert.True(registry.IsConglomerateClient("portal-a"));
        Assert.False(registry.IsConglomerateClient(null));
        Assert.Equal("customer-a", registry.GetClientTenant("portal-a"));
        Assert.Null(registry.GetClientTenant("missing"));
        Assert.Equal(ConglomerateConstants.PortalClassCustomerOperator, registry.GetPortalClass("portal-a"));
        Assert.Equal(ConglomerateConstants.PortalClassOperator, registry.GetPortalClass(null));
        Assert.Equal(ConglomerateConstants.PortalClassOperator, registry.GetPortalClass("missing"));
        Assert.Equal(ConglomerateConstants.TenantClassCustomer, registry.GetTenantClass("CUSTOMER-A"));
        Assert.Equal(ConglomerateConstants.TenantClassInternal, registry.GetTenantClass("missing"));
        Assert.Equal("GROUP-HQ", registry.GetOperatorHome("customer-a"));
        Assert.Null(registry.GetOperatorHome("missing"));
        Assert.Equal(["portal-a"], registry.GetClientIdsForTenant("CUSTOMER-A"));
        Assert.Empty(registry.GetClientIdsForTenant(""));
        Assert.Equal(["customer-a"], registry.GetCustomerTenantsForOperator("group-hq"));
        Assert.True(registry.IsCustomerTenant("customer-a"));
        Assert.False(registry.IsCustomerTenant("group-hq"));
        Assert.Single(registry.AllowedCrossTenantPairs);
        Assert.Equal("customer-a", registry.GetTenantProfile("CUSTOMER-A")?.Key);
        Assert.Null(registry.GetTenantProfile("missing"));
    }

    [Fact]
    public void Registry_merges_customer_tenant_file_and_its_portal_clients()
    {
        using var temp = new TemporaryDirectory();
        var customerPath = temp.Write("customers.json", """
        {
          "customers": [
            {
              "key": "customer-file",
              "displayName": "File Customer",
              "operatorHome": "group-hq",
              "accountKey": "account-a",
              "accountDisplayName": "Account A",
              "environmentKey": "prod",
              "environmentDisplayName": "Production",
              "contractId": "contract-a",
              "dataRegion": "ap-southeast-1",
              "portalClients": [
                { "clientId": "file-client", "displayName": "File Portal", "portalClass": "end_user" },
                { "clientId": " " }
              ]
            },
            {
              "key": "customer-no-portal",
              "displayName": "No Portal Customer"
            },
            { "key": " ", "displayName": "ignored" },
            { "key": "missing-display", "displayName": " " }
          ],
          "crossTenantPolicy": {
            "defaultDeny": false,
            "allowedPairs": [
              { "source": "group-hq", "targetClass": "customer", "operatorHomeMatch": true, "requiresJit": true, "maxDurationMinutes": 15, "reason": "support", "permissions": ["admin.audit.read", " ", 42] },
              { "source": " ", "target": "ignored" },
              { "source": "valid", "target": " " }
            ]
          }
        }
        """);

        var registry = CreateRegistry(new ConglomerateOptions { CustomerTenantsPath = customerPath });

        Assert.Equal(ConglomerateConstants.TenantClassCustomer, registry.GetTenantClass("customer-file"));
        Assert.Equal("account-a", registry.GetTenantProfile("customer-file")?.AccountKey);
        Assert.Equal("customer-file", registry.GetClientTenant("file-client"));
        Assert.Equal("File Customer", registry.GetTenantProfile("customer-file")?.DisplayName);
        Assert.Equal(ConglomerateConstants.PortalClassEndUser, registry.GetPortalClass("file-client"));
        Assert.Single(registry.AllowedCrossTenantPairs);
        var pair = registry.AllowedCrossTenantPairs[0];
        Assert.Equal("customer", pair.TargetClass);
        Assert.True(pair.OperatorHomeMatch);
        Assert.True(pair.RequiresJit);
        Assert.Equal(15, pair.MaxDurationMinutes);
        Assert.Equal(["admin.audit.read"], pair.Permissions);
    }

    [Fact]
    public void Registry_merges_oidc_and_iam_files_with_defaults()
    {
        using var temp = new TemporaryDirectory();
        var oidcPath = temp.Write("oidc.json", """
        { "clients": [
          { "clientId": "oidc-client", "tenantKey": "tenant-a", "displayName": "OIDC Client" },
          { "clientId": " ", "tenantKey": "tenant-a" },
          { "clientId": "missing-tenant", "tenantKey": " " }
        ] }
        """);
        var iamPath = temp.Write("iam.json", """
        { "crossTenantPolicy": {
          "defaultDeny": true,
          "allowedPairs": [
            { "source": "group-hq", "target": "tenant-a" },
            { "source": "group-hq", "targetClass": "customer", "permissions": [] },
            { "source": " ", "target": "ignored" },
            { "source": "missing-target" }
          ]
        } }
        """);

        var registry = CreateRegistry(new ConglomerateOptions { OidcClientsPath = oidcPath, IamScopesPath = iamPath });

        Assert.True(registry.IsConglomerateClient("oidc-client"));
        Assert.Equal("tenant-a", registry.GetClientTenant("oidc-client"));
        Assert.Equal(ConglomerateConstants.PortalClassOperator, registry.GetPortalClass("oidc-client"));
        Assert.Equal(2, registry.AllowedCrossTenantPairs.Count);
        Assert.Equal(60, registry.AllowedCrossTenantPairs[0].MaxDurationMinutes);
        Assert.Equal(["admin.audit.read"], registry.AllowedCrossTenantPairs[0].Permissions);
        Assert.Equal(["admin.audit.read"], registry.AllowedCrossTenantPairs[1].Permissions);
    }

    [Fact]
    public void Registry_ignores_missing_external_files_and_empty_policy_shapes()
    {
        var options = new ConglomerateOptions
        {
            CustomerTenantsPath = "missing-customers.json",
            OidcClientsPath = "missing-oidc.json",
            IamScopesPath = "missing-iam.json"
        };

        var registry = CreateRegistry(options);

        Assert.Empty(registry.AllowedCrossTenantPairs);
        Assert.Equal(ConglomerateConstants.TenantClassInternal, registry.GetTenantClass("missing"));
    }

    private static ConglomerateTenantRegistry CreateRegistry(ConglomerateOptions options)
    {
        var environment = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        return new ConglomerateTenantRegistry(
            Options.Create(options),
            environment.Object,
            NullLogger<ConglomerateTenantRegistry>.Instance);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "identity-conglomerate-tests", Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string Write(string name, string content)
        {
            var path = Path.Combine(_path, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
                Directory.Delete(_path, recursive: true);
        }
    }
}
