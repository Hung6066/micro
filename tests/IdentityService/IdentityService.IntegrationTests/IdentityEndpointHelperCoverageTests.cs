using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public sealed class IdentityEndpointHelperCoverageTests
{
    private static object? Invoke(Type type, string name, params object?[] args) =>
        type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);

    [Fact]
    public void Scim_helpers_map_users_and_bound_query_values()
    {
        var user = new User
        {
            UserName = "doctor",
            Email = "doctor@example.test",
            FirstName = "An",
            LastName = "Nguyen",
            LicenseNumber = "LIC-1",
            Specialty = "medical",
            CreatedAt = DateTime.UtcNow
        };

        var mapped = Invoke(typeof(ScimEndpoints), "MapToScimUser", user)!;
        Assert.Equal(user.Id.ToString(), mapped.GetType().GetProperty("Id")!.GetValue(mapped));

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?startIndex=0&count=999&filter=userName%20eq%20%22doctor%22");
        var query = Invoke(typeof(ScimEndpoints), "ParseScimQuery", context)!;
        Assert.Equal(1, query.GetType().GetProperty("StartIndex")!.GetValue(query));
        Assert.Equal(200, query.GetType().GetProperty("Count")!.GetValue(query));
        Assert.NotNull(Invoke(typeof(ScimEndpoints), "GetServiceProviderConfig"));
        Assert.NotNull(Invoke(typeof(ScimEndpoints), "GetResourceTypes"));
    }

    [Theory]
    [InlineData("organization", "tenant", true)]
    [InlineData("organization", "organization", false)]
    [InlineData("tenant", "account", true)]
    [InlineData("account", "environment", true)]
    public void Iam_scope_hierarchy_validation_is_deterministic(string parent, string child, bool expected)
    {
        Assert.Equal(expected, Invoke(typeof(IamControlPlaneEndpoints), "IsValidParentKind", parent, child));
    }

    [Fact]
    public void Iam_trust_policy_validation_rejects_malformed_and_accepts_object()
    {
        var invalidArgs = new object?[] { "not-json", null };
        Assert.False((bool)Invoke(typeof(IamControlPlaneEndpoints), "TryValidateTrust", invalidArgs)!);
        Assert.NotNull(invalidArgs[1]);

        var validArgs = new object?[] { "{\"principals\":[{\"type\":\"service\"}]}", null };
        Assert.True((bool)Invoke(typeof(IamControlPlaneEndpoints), "TryValidateTrust", validArgs)!);
        Assert.Null(validArgs[1]);
    }

    [Theory]
    [InlineData("nursing", "Nurse")]
    [InlineData("laboratory", "LabTechnician")]
    [InlineData("unknown", "Provider")]
    [InlineData(null, "Provider")]
    public void Hr_department_mapping_has_safe_default(string? department, string expected)
    {
        Assert.Equal(expected, Invoke(typeof(HrWebhookEndpoints), "MapDepartmentToRole", department));
    }

    [Fact]
    public void Hr_signature_helper_accepts_prefix_and_rejects_tampering()
    {
        const string secret = "01234567890123456789012345678901";
        const string timestamp = "1700000000";
        const string body = "{\"event\":\"employee.updated\"}";
        var signature = HrWebhookAuthenticator.ComputeSignature(secret, timestamp, body);

        Assert.True((bool)Invoke(typeof(HrWebhookAuthenticator), "SignatureMatches", secret, timestamp, body, "sha256=" + signature)!);
        Assert.False((bool)Invoke(typeof(HrWebhookAuthenticator), "SignatureMatches", secret, timestamp, body + "x", signature)!);
    }

    [Fact]
    public void Governance_and_passkey_helpers_normalize_security_values()
    {
        Assert.Equal("member_of_admin", Invoke(typeof(AccessGovernanceEndpoints), "NormalizeRelation", " member.of/admin "));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-1") }, "test"));
        var context = new DefaultHttpContext { User = principal };
        Assert.Equal("user-1", Invoke(typeof(PasskeyEndpoints), "GetUserId", context));
        Assert.StartsWith("hishop:passkey:registration:", Invoke(typeof(PasskeyEndpoints), "OptionsKey", "user-1") as string);

        var remaining = (TimeSpan)Invoke(typeof(PasskeyEndpoints), "GetRemainingNativeMfaLifetime", DateTimeOffset.UtcNow)!;
        Assert.True(remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(5));
    }
}
