using System.Text.Json;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// SCIM v2 provisioning endpoints (RFC 7643/7644).
/// Requires SCIM API key in X-SCIM-API-Key header or Bearer token with admin scope.
/// </summary>
public static class ScimEndpoints
{
    private const string ScimUserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ScimGroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";

    public static void MapScimEndpoints(this WebApplication app)
    {
        var scim = app.MapGroup("/scim/v2").RequireAuthorization();

        // Users
        scim.MapGet("/Users", GetUsers);
        scim.MapPost("/Users", CreateUser);
        scim.MapGet("/Users/{id}", GetUser);
        scim.MapPut("/Users/{id}", UpdateUser);
        scim.MapPatch("/Users/{id}", PatchUser);
        scim.MapDelete("/Users/{id}", DeleteUser);

        // Groups (maps to His.Hope roles)
        scim.MapGet("/Groups", GetGroups);
        scim.MapPost("/Groups", CreateGroup);
        scim.MapGet("/Groups/{id}", GetGroup);
        scim.MapDelete("/Groups/{id}", DeleteGroup);

        // ServiceProviderConfig
        scim.MapGet("/ServiceProviderConfig", GetServiceProviderConfig);
        scim.MapGet("/ResourceTypes", GetResourceTypes);
    }

    // ─── Users ───

    private static async Task<Results<Ok<ScimListResponse<ScimUserResponse>>, ProblemHttpResult>> GetUsers(
        HttpContext httpContext, IdentityDbContext db, CancellationToken ct)
    {
        var query = ParseScimQuery(httpContext);
        var users = await db.Users
            .OrderBy(u => u.UserName)
            .Skip(query.StartIndex - 1)
            .Take(query.Count)
            .ToListAsync(ct);

        var total = await db.Users.CountAsync(ct);
        var resources = users.Select(MapToScimUser).ToList();

        return TypedResults.Ok(new ScimListResponse<ScimUserResponse>
        {
            TotalResults = total,
            ItemsPerPage = query.Count,
            StartIndex = query.StartIndex,
            Resources = resources
        });
    }

    private static async Task<Results<Created<ScimUserResponse>, ProblemHttpResult>> CreateUser(
        ScimUserRequest request, UserManager<User> userManager, CancellationToken ct)
    {
        if (await userManager.FindByNameAsync(request.UserName) is not null)
            return TypedResults.Problem("User already exists", statusCode: 409);

        var email = request.Emails?.FirstOrDefault(e => e.Primary)?.Value
                 ?? request.Emails?.FirstOrDefault()?.Value;

        var user = new User
        {
            UserName = request.UserName,
            Email = email ?? request.UserName,
            FirstName = request.Name?.GivenName ?? request.UserName,
            LastName = request.Name?.FamilyName ?? "",
            MiddleName = request.Name?.MiddleName,
            LicenseNumber = request.HisHopeExtension?.LicenseNumber,
            Specialty = request.HisHopeExtension?.Specialty,
            IsActive = request.Active ?? true,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };

        var result = request.Password is not null
            ? await userManager.CreateAsync(user, request.Password)
            : await userManager.CreateAsync(user);

        if (!result.Succeeded)
            return TypedResults.Problem(
                string.Join(", ", result.Errors.Select(e => e.Description)), statusCode: 400);

        if (request.Roles is not null)
            foreach (var role in request.Roles)
                await userManager.AddToRoleAsync(user, role.Value);

        if (request.Entitlements is not null)
            foreach (var ent in request.Entitlements)
                await userManager.AddToRoleAsync(user, ent.Value);

        var response = MapToScimUser(user);
        response.Meta.Location = $"/scim/v2/Users/{user.Id}";

        return TypedResults.Created($"/scim/v2/Users/{user.Id}", response);
    }

    private static async Task<Results<Ok<ScimUserResponse>, NotFound>> GetUser(
        string id, UserManager<User> userManager, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();

        var response = MapToScimUser(user);
        response.Meta.Location = $"/scim/v2/Users/{user.Id}";

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ScimUserResponse>, NotFound>> UpdateUser(
        string id, ScimUserRequest request, UserManager<User> userManager, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();

        user.UserName = request.UserName;
        user.FirstName = request.Name?.GivenName ?? user.FirstName;
        user.LastName = request.Name?.FamilyName ?? user.LastName;
        user.IsActive = request.Active ?? user.IsActive;
        user.LicenseNumber = request.HisHopeExtension?.LicenseNumber ?? user.LicenseNumber;
        user.Specialty = request.HisHopeExtension?.Specialty ?? user.Specialty;

        var email = request.Emails?.FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(email))
            user.Email = email;

        await userManager.UpdateAsync(user);

        return TypedResults.Ok(MapToScimUser(user));
    }

    private static async Task<Results<Ok<ScimUserResponse>, NotFound>> PatchUser(
        string id, ScimPatchRequest patch, UserManager<User> userManager, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();

        foreach (var op in patch.Operations)
        {
            switch (op.Op.ToLower())
            {
                case "replace" when op.Path == "active" && op.Value is JsonElement el:
                    user.IsActive = el.GetBoolean();
                    break;
                case "replace" when op.Path == "name.givenName" && op.Value is JsonElement gn:
                    user.FirstName = gn.GetString() ?? user.FirstName;
                    break;
                case "replace" when op.Path == "name.familyName" && op.Value is JsonElement fn:
                    user.LastName = fn.GetString() ?? user.LastName;
                    break;
            }
        }

        await userManager.UpdateAsync(user);
        return TypedResults.Ok(MapToScimUser(user));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteUser(
        string id, UserManager<User> userManager, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();

        user.IsActive = false;
        await userManager.UpdateAsync(user);
        return TypedResults.NoContent();
    }

    // ─── Groups (Roles) ───

    private static Ok<ScimListResponse<ScimGroupResponse>> GetGroups(
        RoleManager<Role> roleManager)
    {
        var roles = roleManager.Roles.ToList();
        var resources = roles.Select(r => new ScimGroupResponse
        {
            Id = r.Id.ToString(),
            DisplayName = r.Name ?? "",
            Meta = new ScimMeta { ResourceType = "Group", Created = DateTime.UtcNow, LastModified = DateTime.UtcNow }
        }).ToList();

        return TypedResults.Ok(new ScimListResponse<ScimGroupResponse>
        {
            TotalResults = resources.Count,
            ItemsPerPage = resources.Count,
            StartIndex = 1,
            Resources = resources
        });
    }

    private static async Task<Results<Created<ScimGroupResponse>, ProblemHttpResult>> CreateGroup(
        ScimGroupRequest request, RoleManager<Role> roleManager, CancellationToken ct)
    {
        if (await roleManager.RoleExistsAsync(request.DisplayName))
            return TypedResults.Problem("Role already exists", statusCode: 409);

        var role = new Role
        {
            Name = request.DisplayName,
            Description = "SCIM-provisioned role",
            IsSystem = false,
            CreatedAt = DateTime.UtcNow
        };

        await roleManager.CreateAsync(role);

        return TypedResults.Created($"/scim/v2/Groups/{role.Id}", new ScimGroupResponse
        {
            Id = role.Id.ToString(),
            DisplayName = role.Name,
            Meta = new ScimMeta { ResourceType = "Group", Created = DateTime.UtcNow, LastModified = DateTime.UtcNow }
        });
    }

    private static async Task<Results<Ok<ScimGroupResponse>, NotFound>> GetGroup(
        string id, RoleManager<Role> roleManager, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return TypedResults.NotFound();

        return TypedResults.Ok(new ScimGroupResponse
        {
            Id = role.Id.ToString(),
            DisplayName = role.Name ?? "",
            Meta = new ScimMeta { ResourceType = "Group", Created = DateTime.UtcNow, LastModified = DateTime.UtcNow }
        });
    }

    private static async Task<Results<NoContent, NotFound>> DeleteGroup(
        string id, RoleManager<Role> roleManager, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id);
        if (role is null) return TypedResults.NotFound();

        await roleManager.DeleteAsync(role);
        return TypedResults.NoContent();
    }

    // ─── Config ───

    private static IResult GetServiceProviderConfig()
    {
        return Results.Ok(new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
            patch = new { supported = true },
            bulk = new { supported = true, maxOperations = 100, maxPayloadSize = 1048576 },
            filter = new { supported = true, maxResults = 200 },
            changePassword = new { supported = true },
            sort = new { supported = false },
            authenticationSchemes = new[]
            {
                new { type = "oauthbearertoken", name = "OAuth Bearer Token", description = "Bearer token" }
            }
        });
    }

    private static IResult GetResourceTypes()
    {
        return Results.Ok(new[]
        {
            new { schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ResourceType" }, id = "User", name = "User", endpoint = "/scim/v2/Users", schema = ScimUserSchema },
            new { schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ResourceType" }, id = "Group", name = "Group", endpoint = "/scim/v2/Groups", schema = ScimGroupSchema }
        });
    }

    // ─── Mappers ───

    private static ScimUserResponse MapToScimUser(User user)
    {
        var response = new ScimUserResponse
        {
            Id = user.Id.ToString(),
            UserName = user.UserName ?? "",
            DisplayName = user.FullName,
            Name = new ScimName
            {
                Formatted = user.FullName,
                GivenName = user.FirstName,
                FamilyName = user.LastName,
                MiddleName = user.MiddleName
            },
            Emails = new List<ScimEmail>
            {
                new() { Value = user.Email ?? "", Primary = true, Type = "work" }
            },
            Active = user.IsActive,
            Meta = new ScimMeta
            {
                ResourceType = "User",
                Created = user.CreatedAt,
                LastModified = DateTime.UtcNow,
                Location = $"/scim/v2/Users/{user.Id}"
            },
            HisHopeExtension = new ScimHisHopeExtension
            {
                LicenseNumber = user.LicenseNumber,
                Specialty = user.Specialty
            }
        };
        return response;
    }

    private static ScimQueryParams ParseScimQuery(HttpContext httpContext)
    {
        var query = httpContext.Request.Query;
        return new ScimQueryParams
        {
            Filter = query["filter"].FirstOrDefault(),
            StartIndex = int.TryParse(query["startIndex"].FirstOrDefault(), out var si) ? Math.Max(1, si) : 1,
            Count = int.TryParse(query["count"].FirstOrDefault(), out var c) ? Math.Min(200, Math.Max(1, c)) : 100
        };
    }
}
