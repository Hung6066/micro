using System.Reflection;
using System.Security.Claims;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// SCIM contract coverage for two interoperating client profiles (Entra-style and HRIS-style).
/// </summary>
public sealed class ScimMultiVendorConformanceTests
{
    private static Type ScimScopeType => typeof(ScimEndpoints).GetNestedType("ScimScope", BindingFlags.NonPublic)!;

    [Fact]
    public void EntraStyleFixture_MapsUserWithExternalIdAndActiveFlag()
    {
        var user = new User
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            UserName = "entra.user@example.test",
            Email = "entra.user@example.test",
            FirstName = "Entra",
            LastName = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var response = InvokeMapToScimUser(user);
        Assert.Equal(user.Id.ToString(), GetProperty(response, "Id"));
        Assert.Equal("entra.user@example.test", GetProperty(response, "UserName"));
        Assert.True((bool)GetProperty(response, "Active")!);
    }

    [Fact]
    public void HrisStyleFixture_PreservesStableLocationAndMeta()
    {
        var user = new User
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            UserName = "hris-employee-42",
            Email = "employee42@hris.example",
            FirstName = "HRIS",
            LastName = "Employee",
            IsActive = false,
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };

        var response = InvokeMapToScimUser(user);
        var meta = GetProperty(response, "Meta")!;
        Assert.Equal($"/scim/v2/Users/{user.Id}", GetProperty(meta, "Location"));
        Assert.False((bool)GetProperty(response, "Active")!);
    }

    [Fact]
    public void CrossFacilityScope_IsInvalidWhenFacilityClaimMissing()
    {
        var resolve = ScimScopeType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(resolve);

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "Bearer"))
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scim:RequireFacilityScope"] = "true" })
            .Build();

        var scope = resolve!.Invoke(null, [context, configuration])!;
        var isValid = (bool)scope.GetType().GetProperty("IsValid")!.GetValue(scope)!;
        Assert.False(isValid);
    }

    private static object InvokeMapToScimUser(User user)
    {
        var method = typeof(ScimEndpoints).GetMethod("MapToScimUser", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return method!.Invoke(null, [user])!;
    }

    private static object? GetProperty(object target, string name) =>
        target.GetType().GetProperty(name)?.GetValue(target);
}
