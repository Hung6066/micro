using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using His.Hope.Contracts;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// Exercises the SCIM handlers with the real UserManager, RoleManager and
/// PostgreSQL-backed IdentityDbContext.  These tests deliberately call the
/// mapped handler methods so facility authorization and persistence branches
/// are covered without requiring a machine-token issuer in the test host.
/// </summary>
[Collection("IdentityServiceIntegration")]
public sealed class ScimEndpointBranchTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public ScimEndpointBranchTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetUsers_AppliesStartIndexAndCountAgainstPersistedUsers()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var prefix = $"scim-page-{Guid.NewGuid():N}";

        await CreateUserAsync(userManager, $"{prefix}-a", "a@example.test");
        await CreateUserAsync(userManager, $"{prefix}-b", "b@example.test");
        await CreateUserAsync(userManager, $"{prefix}-c", "c@example.test");

        var context = ScimContext();
        context.Request.QueryString = new QueryString($"?startIndex=2&count=1&filter=userName%20eq%20%22{prefix}-b%22");
        var result = await InvokeAsync("GetUsers", context, db, configuration, CancellationToken.None);

        Assert.Equal("Ok`1", result.GetType().Name);
        var value = ResultValue(result);
        Assert.Equal(1, (int)value.GetType().GetProperty("ItemsPerPage")!.GetValue(value)!);
        Assert.Equal(2, (int)value.GetType().GetProperty("StartIndex")!.GetValue(value)!);
        Assert.True((int)value.GetType().GetProperty("TotalResults")!.GetValue(value)! >= 3);
        var resources = (System.Collections.IEnumerable)value.GetType().GetProperty("Resources")!.GetValue(value)!;
        Assert.Single(resources.Cast<object>());
    }

    [Fact]
    public async Task GetUsers_WithRequiredFacilityScopeAndNoClaim_ReturnsFacilityTokenRequired()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scim:RequireFacilityScope"] = "true" })
            .Build();

        var result = await InvokeAsync("GetUsers", ScimContext(), db, configuration, CancellationToken.None);

        Assert.Equal("ProblemHttpResult", result.GetType().Name);
        Assert.Equal(403, result.GetType().GetProperty("StatusCode")!.GetValue(result));
        var extensions = ProblemExtensions(result);
        Assert.Equal(ApiErrorCodes.ScimFacilityTokenRequired, extensions[ApiProblemExtensions.ErrorCode]);
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsConflictProblem()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var username = $"scim-duplicate-{Guid.NewGuid():N}";
        await CreateUserAsync(userManager, username, $"{username}@example.test");

        var result = await InvokeAsync("CreateUser", UserRequest(username), userManager, db, ScimContext(), configuration, CancellationToken.None);

        Assert.Equal("ProblemHttpResult", result.GetType().Name);
        Assert.Equal(409, result.GetType().GetProperty("StatusCode")!.GetValue(result));
    }

    [Fact]
    public async Task CreateUser_WithFacilityScopeOutsideClaim_IsForbidden()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scim:RequireFacilityScope"] = "true" })
            .Build();
        var context = ScimContext(new Claim("facility_id", "FAC-ALLOWED"));

        var result = await InvokeAsync("CreateUser", UserRequest($"scim-denied-{Guid.NewGuid():N}", "FAC-OTHER"), userManager, db, context, configuration, CancellationToken.None);

        Assert.Equal("ProblemHttpResult", result.GetType().Name);
        Assert.Equal(403, result.GetType().GetProperty("StatusCode")!.GetValue(result));
        var extensions = ProblemExtensions(result);
        Assert.Equal(ApiErrorCodes.ScimFacilityScopeDenied, extensions[ApiProblemExtensions.ErrorCode]);
    }

    [Fact]
    public async Task CreatePatchUpdateAndDeleteUser_ChangesPersistedIdentityState()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var username = $"scim-lifecycle-{Guid.NewGuid():N}";

        var created = await InvokeAsync("CreateUser", UserRequest(username), userManager, db, ScimContext(), configuration, CancellationToken.None);
        Assert.Equal("Created`1", created.GetType().Name);
        var createdResponse = ResultValue(created);
        var id = (string)createdResponse.GetType().GetProperty("Id")!.GetValue(createdResponse)!;

        using var falseDocument = JsonDocument.Parse("false");
        using var givenNameDocument = JsonDocument.Parse("\"Patched\"");
        var patch = new ScimPatchRequest
        {
            Operations =
            [
                new ScimPatchOperation { Op = "replace", Path = "active", Value = falseDocument.RootElement.Clone() },
                new ScimPatchOperation { Op = "replace", Path = "name.givenName", Value = givenNameDocument.RootElement.Clone() }
            ]
        };
        var patched = await InvokeAsync("PatchUser", id, patch, userManager, db, ScimContext(), configuration, CancellationToken.None);
        var patchedResponse = ResultValue(patched);
        Assert.False((bool)patchedResponse.GetType().GetProperty("Active")!.GetValue(patchedResponse)!);
        Assert.Equal("Patched", ((object)patchedResponse.GetType().GetProperty("Name")!.GetValue(patchedResponse)!).GetType().GetProperty("GivenName")!.GetValue(patchedResponse.GetType().GetProperty("Name")!.GetValue(patchedResponse)));

        var updated = await InvokeAsync("UpdateUser", id, UserRequest(username, givenName: "Updated"), userManager, db, ScimContext(), configuration, CancellationToken.None);
        Assert.Equal("Ok`1", updated.GetType().Name);
        var deleted = await InvokeAsync("DeleteUser", id, userManager, db, ScimContext(), configuration, CancellationToken.None);
        Assert.Equal("NoContent", deleted.GetType().Name);
        var persisted = await userManager.FindByIdAsync(id);
        Assert.NotNull(persisted);
        Assert.False(persisted!.IsActive);
    }

    [Fact]
    public async Task UserHandlers_ReturnNotFoundForUnknownOrOutOfFacilityUser()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scim:RequireFacilityScope"] = "true" })
            .Build();
        var user = await CreateUserAsync(userManager, $"scim-facility-{Guid.NewGuid():N}", "facility@example.test");
        db.UserFacilities.Add(new UserFacility { UserId = user.Id, FacilityId = "FAC-OTHER", IsPrimary = true });
        await db.SaveChangesAsync();

        var context = ScimContext(new Claim("facility_id", "FAC-ALLOWED"));
        var get = await InvokeAsync("GetUser", user.Id.ToString(), userManager, db, context, configuration, CancellationToken.None);
        var patch = await InvokeAsync("PatchUser", Guid.NewGuid().ToString(), new ScimPatchRequest(), userManager, db, ScimContext(), configuration, CancellationToken.None);

        Assert.Equal("NotFound", get.GetType().Name);
        Assert.Equal("NotFound", patch.GetType().Name);
    }

    [Fact]
    public async Task GroupHandlers_CreateDuplicateGetAndDeleteRole()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var roleName = $"SCIM-{Guid.NewGuid():N}";
        var request = new ScimGroupRequest { DisplayName = roleName };

        var created = await InvokeAsync("CreateGroup", request, roleManager, CancellationToken.None);
        Assert.Equal("Created`1", created.GetType().Name);
        var id = (string)ResultValue(created).GetType().GetProperty("Id")!.GetValue(ResultValue(created))!;
        var duplicate = await InvokeAsync("CreateGroup", request, roleManager, CancellationToken.None);
        Assert.Equal(409, duplicate.GetType().GetProperty("StatusCode")!.GetValue(duplicate));
        var found = await InvokeAsync("GetGroup", id, roleManager, CancellationToken.None);
        Assert.Equal("Ok`1", found.GetType().Name);
        var deleted = await InvokeAsync("DeleteGroup", id, roleManager, CancellationToken.None);
        Assert.Equal("NoContent", deleted.GetType().Name);
        Assert.False(await roleManager.RoleExistsAsync(roleName));
    }

    private static HttpContext ScimContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity([new Claim("scope", "scim.read scim.write"), .. claims], "Bearer");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static ScimUserRequest UserRequest(string username, string? facilityId = null, string? givenName = null) =>
        new()
        {
            UserName = username,
            Name = new ScimName { GivenName = givenName ?? "SCIM", FamilyName = "Test" },
            Emails = [new ScimEmail { Value = $"{username}@example.test", Primary = true }],
            HisHopeExtension = facilityId is null ? null : new ScimHisHopeExtension { FacilityId = facilityId }
        };

    private static async Task<User> CreateUserAsync(UserManager<User> manager, string username, string email)
    {
        var user = new User
        {
            UserName = username,
            Email = email,
            FirstName = "Seed",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        var result = await manager.CreateAsync(user);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
        return user;
    }

    private static object ResultValue(object result) =>
        result.GetType().GetProperty("Value")?.GetValue(result)
        ?? throw new InvalidOperationException($"SCIM result {result.GetType().Name} has no Value.");

    private static IDictionary<string, object?> ProblemExtensions(object result)
    {
        var details = result.GetType().GetProperty("ProblemDetails")?.GetValue(result)
            ?? throw new InvalidOperationException("SCIM problem result has no ProblemDetails.");
        return (IDictionary<string, object?>)details.GetType().GetProperty("Extensions")!.GetValue(details)!;
    }

    private static async Task<object> InvokeAsync(string methodName, params object?[] args)
    {
        var method = typeof(ScimEndpoints).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(ScimEndpoints).FullName, methodName);
        var task = (Task)(method.Invoke(null, args) ?? throw new InvalidOperationException("SCIM handler returned null."));
        await task;
        var result = task.GetType().GetProperty("Result")?.GetValue(task)
            ?? throw new InvalidOperationException("SCIM handler task returned no result.");
        // Minimal APIs return Results<TSuccess, TFailure>; unwrap its
        // discriminated result so assertions exercise the actual HTTP result.
        return result.GetType().GetProperty("Result")?.GetValue(result) ?? result;
    }
}
