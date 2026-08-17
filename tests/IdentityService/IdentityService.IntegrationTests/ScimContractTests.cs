using System.Reflection;
using System.Security.Claims;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class ScimContractTests
{
    private static Type ScimScopeType => typeof(ScimEndpoints).GetNestedType("ScimScope", BindingFlags.NonPublic)!;

    [Fact]
    public void Scim_user_mapper_preserves_identity_fields_and_safe_location()
    {
        var method = typeof(ScimEndpoints).GetMethod("MapToScimUser", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "scim-user",
            Email = "scim@example.test",
            FirstName = "Scim",
            LastName = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var response = method!.Invoke(null, [user])!;
        Assert.Equal(user.Id.ToString(), response.GetType().GetProperty("Id")!.GetValue(response));
        Assert.Equal("scim-user", response.GetType().GetProperty("UserName")!.GetValue(response));
        var meta = response.GetType().GetProperty("Meta")!.GetValue(response)!;
        Assert.Equal($"/scim/v2/Users/{user.Id}", meta.GetType().GetProperty("Location")!.GetValue(meta));
    }

    [Fact]
    public void Scim_query_parser_clamps_pagination_to_rfc_safe_bounds()
    {
        var method = typeof(ScimEndpoints).GetMethod("ParseScimQuery", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?startIndex=0&count=9999&filter=userName%20eq%20%5C%22scim%5C%22");

        var query = method!.Invoke(null, [context])!;

        Assert.Equal(1, query.GetType().GetProperty("StartIndex")!.GetValue(query));
        Assert.Equal(200, query.GetType().GetProperty("Count")!.GetValue(query));
        Assert.Equal("userName eq \\\"scim\\\"", query.GetType().GetProperty("Filter")!.GetValue(query));
    }

    [Fact]
    public void Scim_query_parser_uses_safe_defaults_for_invalid_pagination()
    {
        var method = typeof(ScimEndpoints).GetMethod("ParseScimQuery", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?startIndex=not-a-number&count=-4");

        var query = method!.Invoke(null, [context])!;

        Assert.Equal(1, query.GetType().GetProperty("StartIndex")!.GetValue(query));
        Assert.Equal(1, query.GetType().GetProperty("Count")!.GetValue(query));
        Assert.Null(query.GetType().GetProperty("Filter")!.GetValue(query));
    }

    [Fact]
    public void Scim_scope_requires_facility_when_enforcement_is_enabled()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("facility_id", "FAC-1")
            }, "Bearer"))
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scim:RequireFacilityScope"] = "true"
            })
            .Build();

        var resolve = ScimScopeType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!;
        var scope = resolve.Invoke(null, [context, configuration])!;

        Assert.True((bool)ScimScopeType.GetProperty("IsValid")!.GetValue(scope)!);
        Assert.True((bool)ScimScopeType.GetMethod("CanWrite")!.Invoke(scope, ["FAC-1"])!);
        Assert.False((bool)ScimScopeType.GetMethod("CanWrite")!.Invoke(scope, ["FAC-2"])!);
    }

    [Fact]
    public void Scim_scope_accepts_machine_facility_claims_and_normalizes_csv_values()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("scope", "scim.write"),
                new Claim("scim_facility_ids", " FAC-1, FAC-2 ")
            }, "Bearer"))
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scim:RequireFacilityScope"] = "true"
            })
            .Build();

        var scope = ScimScopeType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [context, configuration])!;
        var canWrite = ScimScopeType.GetMethod("CanWrite")!;

        Assert.True((bool)canWrite.Invoke(scope, ["FAC-1"])!);
        Assert.True((bool)canWrite.Invoke(scope, ["FAC-2"])!);
        Assert.False((bool)canWrite.Invoke(scope, [null])!);
    }

    [Fact]
    public void Scim_scope_without_enforcement_is_cross_facility_for_legacy_clients()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        var configuration = new ConfigurationBuilder().Build();

        var scope = ScimScopeType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [context, configuration])!;

        Assert.True((bool)ScimScopeType.GetProperty("IsValid")!.GetValue(scope)!);
        Assert.True((bool)ScimScopeType.GetMethod("CanWrite")!.Invoke(scope, [null])!);
    }
}
