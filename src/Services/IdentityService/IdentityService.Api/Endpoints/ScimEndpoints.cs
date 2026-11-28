using System.Text.Json;
using System.Threading.RateLimiting;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using His.Hope.Authorization;
using His.Hope.Contracts.Identity;
using His.Hope.Contracts;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// SCIM v2 provisioning endpoints (RFC 7643/7644).
/// Requires a bearer token issued for a registered SCIM machine client with
/// scim.read/scim.write scope. Admin-cookie compatibility is test/development only.
/// </summary>
public static class ScimEndpoints
{
    private const string ScimUserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    private const string ScimGroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";

    public static void MapScimEndpoints(this WebApplication app)
    {
        var scimMetadata = app.MapGroup(IdentityApiRoutes.ScimV2);
        scimMetadata.MapGet("/ServiceProviderConfig", GetServiceProviderConfig).AllowAnonymous();
        scimMetadata.MapGet("/ResourceTypes", GetResourceTypes).AllowAnonymous();

        // RequireAuthorization returns the same builder, so read and write need
        // their own groups; sharing one made every GET require the write scope.
        RouteGroupBuilder ScimGroup() =>
            app.MapGroup(IdentityApiRoutes.ScimV2).RequireAuthorization("ScimM2M").RequireRateLimiting("scim");
        var scimRead = ScimGroup().RequireAuthorization("ScimM2MRead");
        var scimWrite = ScimGroup().RequireAuthorization("ScimM2MWrite");

        // Users
        scimRead.MapGet("/Users", GetUsers);
        scimWrite.MapPost("/Users", CreateUser);
        scimRead.MapGet("/Users/{id}", GetUser);
        scimWrite.MapPut("/Users/{id}", UpdateUser);
        scimWrite.MapPatch("/Users/{id}", PatchUser);
        scimWrite.MapDelete("/Users/{id}", DeleteUser);

        // Groups (maps to His.Hope roles)
        scimRead.MapGet("/Groups", GetGroups);
        scimWrite.MapPost("/Groups", CreateGroup);
        scimRead.MapGet("/Groups/{id}", GetGroup);
        scimWrite.MapDelete("/Groups/{id}", DeleteGroup);

    }

    // ─── Users ───

    private static async Task<Results<Ok<ScimListResponse<ScimUserResponse>>, ProblemHttpResult>> GetUsers(
        HttpContext httpContext, IdentityDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var scope = ScimScope.Resolve(httpContext, configuration);
        if (!scope.IsValid)
            return TypedResults.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ScimFacilityTokenRequired });
        var query = ParseScimQuery(httpContext);
        var usersQuery = db.Users.AsNoTracking();
        if (scope.IsEnforced && !scope.IsCrossFacility)
            usersQuery = usersQuery.Where(user => user.FacilityMemberships.Any(membership => membership.IsActive && scope.FacilityIds.Contains(membership.FacilityId)));

        // Directories look an account up with a filter before creating it. Ignoring
        // the filter returned the whole (unfiltered) page, so the wrong account could
        // be matched. Supported: userName / emails.value / id / externalId "eq".
        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                query.Filter.Trim(),
                "^(userName|emails\\.value|emails\\[type eq \"work\"\\]\\.value|id|externalId)\\s+eq\\s+\"([^\"]*)\"$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(200));
            if (!match.Success)
                return TypedResults.Problem(statusCode: 400, detail: "Unsupported SCIM filter.",
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Validation, ["scimType"] = "invalidFilter" });
            var attribute = match.Groups[1].Value.ToLowerInvariant();
            var operand = match.Groups[2].Value;
            var normalized = operand.ToUpperInvariant();
            if (attribute == "username")
                usersQuery = usersQuery.Where(user => user.NormalizedUserName == normalized);
            else if (attribute.StartsWith("emails", StringComparison.Ordinal))
                usersQuery = usersQuery.Where(user => user.NormalizedEmail == normalized);
            else if (Guid.TryParse(operand, out var requestedId))
                usersQuery = usersQuery.Where(user => user.Id == requestedId);
            else
                usersQuery = usersQuery.Where(user => false);
        }

        var users = await usersQuery
            .OrderBy(u => u.UserName)
            .Skip(query.StartIndex - 1)
            .Take(query.Count)
            .ToListAsync(ct);

        var total = await usersQuery.CountAsync(ct);
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
        ScimUserRequest request, UserManager<User> userManager, IdentityDbContext db, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        var scope = ScimScope.Resolve(httpContext, configuration);
        if (!scope.IsValid || !scope.CanWrite(request.HisHopeExtension?.FacilityId))
            return TypedResults.Problem(statusCode: 403, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ScimFacilityScopeDenied });
        if (await userManager.FindByNameAsync(request.UserName) is not null)
            return TypedResults.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ScimUserAlreadyExists });

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
        {
            var errors = result.Errors.ToArray();
            var duplicateEmail = errors.Any(error =>
                error.Code.Equals("DuplicateEmail", StringComparison.OrdinalIgnoreCase));
            return TypedResults.Problem(
                string.Join(", ", errors.Select(e => e.Description)),
                statusCode: duplicateEmail ? 409 : 400);
        }

        // SECURITY: a provisioning credential must not be able to mint an
        // administrator. Roles carrying any admin.* permission are assigned only
        // through the governed admin API.
        var administrativeRoles = (await db.RolePermissions.AsNoTracking()
                .Where(link => link.PermissionCode.StartsWith("admin."))
                .Select(link => link.Role.Name!)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.Roles is not null)
            foreach (var role in request.Roles.Where(item => !administrativeRoles.Contains(item.Value)))
                await userManager.AddToRoleAsync(user, role.Value);

        if (request.Entitlements is not null)
            foreach (var ent in request.Entitlements.Where(item => !administrativeRoles.Contains(item.Value)))
                await userManager.AddToRoleAsync(user, ent.Value);

        await AfterUserWriteAsync(httpContext, user, user.IsActive, "SCIM_CREATE", ct);

        if (!string.IsNullOrWhiteSpace(request.HisHopeExtension?.FacilityId))
        {
            db.UserFacilities.Add(new UserFacility { UserId = user.Id, FacilityId = request.HisHopeExtension.FacilityId!, IsPrimary = true });
            await db.SaveChangesAsync(ct);
        }

        var response = MapToScimUser(user);
        response.Meta.Location = $"/scim/v2/Users/{user.Id}";

        return TypedResults.Created($"/scim/v2/Users/{user.Id}", response);
    }

    private static async Task<Results<Ok<ScimUserResponse>, NotFound>> GetUser(
        string id, UserManager<User> userManager, IdentityDbContext db, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();
        var scope = ScimScope.Resolve(httpContext, configuration);
        if (!scope.IsValid || !await scope.CanAccessUserAsync(db, user.Id, ct)) return TypedResults.NotFound();

        var response = MapToScimUser(user);
        response.Meta.Location = $"/scim/v2/Users/{user.Id}";

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<ScimUserResponse>, NotFound>> UpdateUser(
        string id, ScimUserRequest request, UserManager<User> userManager, IdentityDbContext db, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();
        var scope = ScimScope.Resolve(httpContext, configuration);
        if (!scope.IsValid || !await scope.CanAccessUserAsync(db, user.Id, ct) || !scope.CanWrite(request.HisHopeExtension?.FacilityId)) return TypedResults.NotFound();

        var wasActive = user.IsActive;
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
        await AfterUserWriteAsync(httpContext, user, wasActive, "SCIM_UPDATE", ct);

        return TypedResults.Ok(MapToScimUser(user));
    }

    private static async Task<Results<Ok<ScimUserResponse>, NotFound>> PatchUser(
        string id, ScimPatchRequest patch, UserManager<User> userManager, IdentityDbContext db, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();
        var scope = ScimScope.Resolve(httpContext, configuration);
        if (!scope.IsValid || !await scope.CanAccessUserAsync(db, user.Id, ct)) return TypedResults.NotFound();

        var wasActive = user.IsActive;
        foreach (var op in patch.Operations.Where(item => !string.IsNullOrWhiteSpace(item.Op)))
        {
            switch (op.Op.ToLower())
            {
                case "replace" when op.Path == "active" && op.Value is JsonElement el:
                    // Directories send booleans and the strings "True"/"False"; a
                    // deprovisioning call must not fail on the latter.
                    user.IsActive = el.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String when bool.TryParse(el.GetString(), out var parsed) => parsed,
                        _ => user.IsActive
                    };
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
        await AfterUserWriteAsync(httpContext, user, wasActive, "SCIM_PATCH", ct);
        return TypedResults.Ok(MapToScimUser(user));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteUser(
        string id, UserManager<User> userManager, IdentityDbContext db, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return TypedResults.NotFound();
        var scope = ScimScope.Resolve(httpContext, configuration);
        if (!scope.IsValid || !await scope.CanAccessUserAsync(db, user.Id, ct)) return TypedResults.NotFound();

        var wasActive = user.IsActive;
        user.IsActive = false;
        await userManager.UpdateAsync(user);
        await AfterUserWriteAsync(httpContext, user, wasActive, "SCIM_DELETE", ct);
        return TypedResults.NoContent();
    }

    /// <summary>Deprovisioning ends the user's sessions; every SCIM user write is audited.</summary>
    private static async Task AfterUserWriteAsync(
        HttpContext http, User user, bool wasActive, string action, CancellationToken ct)
    {
        // RequestServices is always set for a real request; direct handler
        // invocation (unit-style tests) has no scope to resolve from.
        if (http.RequestServices is not { } services)
            return;
        if (wasActive && !user.IsActive)
            await His.Hope.IdentityService.Api.Services.UserSessionRevocation.RevokeAllAsync(services, user.Id, ct);
        if (services.GetService<His.Hope.Infrastructure.Audit.IAuditService>() is { } audit)
            await AdminAudit.LogAsync(audit, http, action, "User", user.Id.ToString(), ct);
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
            return TypedResults.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ScimRoleAlreadyExists });

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
        // System roles are not SCIM-managed groups; deleting one would strip the
        // role from every holder.
        if (role is null || role.IsSystem) return TypedResults.NotFound();

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

    private sealed record ScimScope(IReadOnlySet<string> FacilityIds, bool IsCrossFacility, bool IsEnforced)
    {
        public bool IsValid => !IsEnforced || IsCrossFacility || FacilityIds.Count > 0;

        public bool CanWrite(string? facilityId) => !IsEnforced || IsCrossFacility || (!string.IsNullOrWhiteSpace(facilityId) && FacilityIds.Contains(facilityId));

        public Task<bool> CanAccessUserAsync(IdentityDbContext db, Guid userId, CancellationToken ct) =>
            !IsEnforced || IsCrossFacility
                ? Task.FromResult(true)
                : db.UserFacilities.AsNoTracking().AnyAsync(item => item.UserId == userId && item.IsActive && FacilityIds.Contains(item.FacilityId), ct);

        public static ScimScope Resolve(HttpContext httpContext, IConfiguration configuration)
        {
            // SECURITY: outside Development/Testing a SCIM client is confined to the
            // facilities carried by its token unless configuration says otherwise.
            // The old default (off) let every SCIM client read and write every tenant.
            var environment = httpContext.RequestServices?.GetService<IHostEnvironment>();
            var enforced = configuration.GetValue(
                "Scim:RequireFacilityScope",
                environment is not null && !environment.IsDevelopment() && !environment.IsEnvironment("Testing"));
            var principal = httpContext.User;
            var scope = FacilityAccessScope.FromPrincipal(principal);
            // Machine clients do not have a human user's memberships. Allow
            // their registration to carry an explicit SCIM facility claim,
            // while retaining the normal user-token claims for delegated use.
            var machineFacilities = principal.FindAll("scim_facility_ids")
                .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Concat(principal.FindAll("scim_facility_id").Select(claim => claim.Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (machineFacilities.Count > 0)
                return new ScimScope(machineFacilities, scope.IsCrossFacility, enforced);

            return new ScimScope(scope.FacilityIds, scope.IsCrossFacility, enforced);
        }
    }
}
