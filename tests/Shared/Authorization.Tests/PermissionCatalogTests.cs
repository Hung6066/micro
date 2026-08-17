using FluentAssertions;
using His.Hope.Authorization.Handlers;
using His.Hope.SharedKernel.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class PermissionCatalogTests
{
    [Fact]
    public void Catalog_codes_are_unique_and_have_descriptors()
    {
        HisHopePermissions.All.Should().OnlyHaveUniqueItems();
        HisHopePermissions.AllDescriptors.Select(descriptor => descriptor.Code)
            .Should().BeEquivalentTo(HisHopePermissions.All);
    }

    [Fact]
    public void Built_in_role_mappings_only_reference_registered_permissions()
    {
        foreach (var role in new[] { "Admin", "Provider", "Nurse", "Receptionist", "LabTechnician", "Pharmacist", "BillingClerk" })
        {
            RolePermissionMapping.GetPermissionsForRoles([role])
                .Should().OnlyContain(permission => HisHopePermissions.IsValid(permission));
        }
    }

    [Fact]
    public async Task Every_catalog_permission_has_a_registered_policy()
    {
        var services = new ServiceCollection()
            .AddHisHopeAuthorization()
            .BuildServiceProvider();
        var provider = services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();

        foreach (var permission in HisHopePermissions.All)
        {
            var policy = await provider.GetPolicyAsync($"Permission:{permission}");
            policy.Should().NotBeNull();
        }
    }
}
