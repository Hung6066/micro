using System.Security.Claims;
using FluentAssertions;
using His.Hope.Authorization;
using His.Hope.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.Authorization.Tests;

public sealed class ScopePolicyCompositionTests
{
    [Fact]
    public async Task Denies_permission_holder_without_workload_scope()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = Principal("admin.settings.write");

        var result = await authorization.AuthorizeAsync(principal, "Continuity.Write");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Human_admin_policy_rejects_workload_principal_even_with_admin_permission()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        (await authorization.AuthorizeAsync(
            Principal("admin.users.read", principalType: "workload"),
            "HumanAdmin"))
            .Succeeded.Should().BeFalse();

        (await authorization.AuthorizeAsync(
            Principal("admin.users.read", principalType: "human"),
            "HumanAdmin"))
            .Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Human_super_admin_policy_requires_admin_role_and_human_principal()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var superAdmin = Principal("admin.users.read", principalType: "human");
        ((ClaimsIdentity)superAdmin.Identity!).AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        ((ClaimsIdentity)superAdmin.Identity!).AddClaim(new Claim("super_admin", "true"));

        (await authorization.AuthorizeAsync(superAdmin, "HumanSuperAdmin"))
            .Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(Principal("admin.users.read", principalType: "human"), "HumanSuperAdmin"))
            .Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(Principal("admin.users.read", principalType: "workload"), "HumanSuperAdmin"))
            .Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Allows_permission_holder_with_explicit_workload_scope()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var principal = Principal("admin.settings.write", "platform.continuity.write", "workload");

        var result = await authorization.AuthorizeAsync(principal, "Continuity.Write");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Continuity_policy_allows_explicitly_typed_human_principal_with_workload_scope()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(
            Principal("admin.settings.write", "platform.continuity.write", "human"),
            "Continuity.Write");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Fhir_patient_policy_requires_patient_scope_in_addition_to_permission()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        (await authorization.AuthorizeAsync(Principal("patients.view"), "Fhir.Patient.Read"))
            .Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(Principal("patients.view", "fhir.patient.read"), "Fhir.Patient.Read"))
            .Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(Principal("patients.view", "fhir.patient.read", "human"), "Fhir.Patient.Read"))
            .Succeeded.Should().BeTrue();
        (await authorization.AuthorizeAsync(Principal("patients.view", "fhir.patient.read", "workload"), "Fhir.Patient.Read"))
            .Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Fhir_encounter_policy_rejects_patient_scope_substitution()
    {
        await using var provider = BuildProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        (await authorization.AuthorizeAsync(Principal("clinical.view", "fhir.patient.read"), "Fhir.Encounter.Read"))
            .Succeeded.Should().BeFalse();
        (await authorization.AuthorizeAsync(Principal("clinical.view", "fhir.encounter.read", "human"), "Fhir.Encounter.Read"))
            .Succeeded.Should().BeTrue();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHisHopeAuthorization();
        services.AddAuthorizationBuilder()
            .AddPolicy("Continuity.Write", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement("admin.settings.write"),
                    new ScopeRequirement("platform.continuity.write"),
                    new PrincipalTypeRequirement("human", "workload")))
            .AddPolicy("Fhir.Patient.Read", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement("patients.view"),
                    new ScopeRequirement("fhir.patient.read"),
                    new PrincipalTypeRequirement("human")))
            .AddPolicy("Fhir.Encounter.Read", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement("clinical.view"),
                    new ScopeRequirement("fhir.encounter.read"),
                    new PrincipalTypeRequirement("human")));
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal Principal(string permission, string? scope = null, string? principalType = null)
    {
        var claims = new List<Claim>
        {
            new("sub", "operator-1"),
            new("permissions", permission)
        };
        if (scope is not null)
            claims.Add(new Claim("scope", scope));
        if (principalType is not null)
            claims.Add(new Claim("principal_type", principalType));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
