using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using His.Hope.SharedKernel.Authorization;
using His.Hope.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Authorization;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Microsoft.IdentityModel.Tokens;

namespace His.Hope.IdentityService.Application.OpenIddict;

/// <summary>Creates a workload principal for the OAuth2 client-credentials grant.</summary>
public sealed class CustomHandleClientCredentialsRequest :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IWorkloadSessionStore _sessionStore;

    public CustomHandleClientCredentialsRequest(IApplicationDbContext dbContext, IWorkloadSessionStore sessionStore)
    {
        _dbContext = dbContext;
        _sessionStore = sessionStore;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<CustomHandleClientCredentialsRequest>()
            .SetOrder(OpenIddictServerHandlers.Exchange.HandleTokenRequest.Descriptor.Order + 100)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        if (!string.Equals(context.Request.GrantType, OpenIddictConstants.GrantTypes.ClientCredentials, StringComparison.Ordinal))
            return;

        if (string.IsNullOrWhiteSpace(context.ClientId))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "A workload client id is required.", null);
            return;
        }

        // OAuth client ids are the stable workload principal. The IAM role key
        // is a human-readable template, while Audience binds the role to the
        // concrete service principal. Never accept permissions from the token
        // request itself.
        var workloadRole = await _dbContext.IamWorkloadRoles.AsNoTracking()
            .SingleOrDefaultAsync(role => role.IsActive &&
                (role.Audience == context.ClientId || role.Key == context.ClientId),
                context.CancellationToken);
        if (workloadRole is null)
        {
            context.Reject(OpenIddictConstants.Errors.UnauthorizedClient, "The workload client has no active IAM workload role.", null);
            return;
        }

        if (!WorkloadTokenExchangePolicy.TrustsActor(workloadRole.TrustPolicyJson, context.ClientId))
        {
            context.Reject(OpenIddictConstants.Errors.UnauthorizedClient, "The workload client is not trusted by its IAM role.", null);
            return;
        }

        var permissions = JsonSerializer.Deserialize<string[]>(workloadRole.PermissionsJson) ?? [];
        var registeredPrefixes = await _dbContext.IamServiceDefinitions.AsNoTracking()
            .Where(item => item.IsActive).Select(item => item.PermissionPrefix)
            .ToListAsync(context.CancellationToken);
        var assignedPermissionJson = await _dbContext.IamPermissionSetAssignments.AsNoTracking()
            .Where(assignment => assignment.PrincipalId == workloadRole.Id &&
                assignment.PrincipalType == AuthorizationConstants.PrincipalTypes.Workload &&
                assignment.Status == AuthorizationConstants.LifecycleStatuses.Active &&
                (assignment.ExpiresAt == null || assignment.ExpiresAt > DateTime.UtcNow))
            .Join(_dbContext.IamPermissionSets.AsNoTracking()
                    .Where(set => set.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published),
                assignment => assignment.PermissionSetId,
                set => set.Id,
                (_, set) => set.PermissionsJson)
            .ToListAsync(context.CancellationToken);
        permissions = permissions
            .Concat(assignedPermissionJson.SelectMany(json => JsonSerializer.Deserialize<string[]>(json) ?? []))
            .Where(code => PermissionCatalogRules.IsValid(code, registeredPrefixes))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var boundary = await _dbContext.IamPermissionBoundaries.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IsActive && item.PrincipalType == AuthorizationConstants.PrincipalTypes.Workload &&
                item.PrincipalId == workloadRole.Id && item.ScopeId == workloadRole.ScopeId,
                context.CancellationToken);
        if (boundary is not null)
        {
            var allowed = JsonSerializer.Deserialize<string[]>(boundary.AllowedPermissionsJson) ?? [];
            permissions = permissions.Intersect(allowed, StringComparer.Ordinal).ToArray();
        }

        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, context.ClientId);
        identity.SetClaim(OpenIddictConstants.Claims.ClientId, context.ClientId);
        identity.SetClaim(AuthorizationConstants.Claims.PrincipalType, AuthorizationConstants.PrincipalTypes.Workload);
        identity.SetClaim("workload_role_id", workloadRole.Id.ToString("D"));
        identity.SetClaim("authorization_version", workloadRole.CreatedAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var workloadSessionId = Guid.NewGuid().ToString("N");
        identity.SetClaim("session_id", workloadSessionId);
        identity.SetClaim("permissions", string.Join(',', permissions));
        if (boundary is not null)
            identity.SetClaim("authorization_constraints", boundary.ResourceConstraintsJson);

        var tenantKey = await _dbContext.IamScopes
            .Where(scope => scope.Id == workloadRole.ScopeId && scope.Kind == "tenant")
            .Select(scope => scope.Key)
            .SingleOrDefaultAsync(context.CancellationToken)
            ?? await _dbContext.IamScopes
                .Where(scope => scope.Id == workloadRole.ScopeId)
                .Join(_dbContext.IamScopes.Where(parent => parent.Kind == "tenant"),
                    scope => scope.ParentId, parent => parent.Id, (_, parent) => parent.Key)
                .SingleOrDefaultAsync(context.CancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantKey))
            identity.SetClaim("tenant_id", tenantKey);
        var workloadPolicies = await ResourcePolicyClaimBuilder.BuildAsync(
            _dbContext, workloadRole.ScopeId,
            [workloadRole.Key, workloadRole.Audience, context.ClientId],
            context.CancellationToken);
        if (workloadPolicies is not null)
            identity.SetClaim("resource_policies", workloadPolicies);

        await _sessionStore.RegisterAsync(new WorkloadSessionRecord(
            workloadSessionId, context.ClientId, workloadRole.Id.ToString("D"),
            DateTime.UtcNow, DateTime.UtcNow.AddSeconds(Math.Clamp(workloadRole.MaxSessionSeconds, 60, 3600))),
            context.CancellationToken);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(context.Request.GetScopes());
        principal.SetAudiences(workloadRole.Audience);
        context.SignIn(principal);
    }
}

/// <summary>
/// RFC 8693-compatible, audience-bound workload exchange. The subject token
/// must be a locally issued, cryptographically valid access token; the target
/// workload role and requested scopes are intersected server-side.
/// </summary>
public sealed class CustomHandleTokenExchangeRequest :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOptionsMonitor<OpenIddictServerOptions> _serverOptions;
    private readonly IWorkloadSessionStore _sessionStore;

    public CustomHandleTokenExchangeRequest(IApplicationDbContext dbContext, IOptionsMonitor<OpenIddictServerOptions> serverOptions, IWorkloadSessionStore sessionStore)
    {
        _dbContext = dbContext;
        _serverOptions = serverOptions;
        _sessionStore = sessionStore;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<CustomHandleTokenExchangeRequest>()
            .SetOrder(OpenIddictServerHandlers.Exchange.HandleTokenRequest.Descriptor.Order + 110)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        if (!string.Equals(context.Request.GrantType, AuthorizationConstants.GrantTypes.TokenExchange, StringComparison.Ordinal))
            return;

        var subjectToken = context.Request.GetParameter("subject_token")?.ToString();
        var subjectTokenType = context.Request.GetParameter("subject_token_type")?.ToString();
        var roleKey = context.Request.GetParameter("requested_role")?.ToString();
        var audience = context.Request.GetParameter("audience")?.ToString()
            ?? context.Request.GetParameter("resource")?.ToString();
        if (string.IsNullOrWhiteSpace(subjectToken) ||
            !string.Equals(subjectTokenType, "urn:ietf:params:oauth:token-type:access_token", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(roleKey) || string.IsNullOrWhiteSpace(audience))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidRequest, "subject_token, subject_token_type, requested_role and audience are required.", null);
            return;
        }

        ClaimsPrincipal source;
        try
        {
            // Keep claim names stable for RFC 8693 processing.  Relying on the
            // process-wide JwtSecurityTokenHandler.DefaultMapInboundClaims
            // makes concurrent token requests (and tests) race on a mutable
            // global and can hide the standard `sub` claim.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            source = handler.ValidateToken(subjectToken, _serverOptions.CurrentValue.TokenValidationParameters, out _);
        }
        catch (Exception)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "The subject token is invalid or expired.", null);
            return;
        }

        var sourceSubject = source.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        var clientId = context.ClientId;
        if (string.IsNullOrWhiteSpace(sourceSubject) || string.IsNullOrWhiteSpace(clientId))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "The subject token has no usable subject or actor.", null);
            return;
        }

        var role = await _dbContext.IamWorkloadRoles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == roleKey && item.IsActive, context.CancellationToken);
        if (role is null || !string.Equals(role.Audience, audience, StringComparison.Ordinal))
        {
            context.Reject("invalid_target", "The requested workload role or audience is not allowed.", null);
            return;
        }

        if (!TrustsActor(role.TrustPolicyJson, clientId))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidGrant, "The actor is not trusted by the requested workload role.", null);
            return;
        }

        var rolePermissions = JsonSerializer.Deserialize<string[]>(role.PermissionsJson) ?? [];
        var registeredPrefixes = await _dbContext.IamServiceDefinitions.AsNoTracking()
            .Where(item => item.IsActive).Select(item => item.PermissionPrefix)
            .ToListAsync(context.CancellationToken);
        var assignedPermissionJson = await _dbContext.IamPermissionSetAssignments.AsNoTracking()
            .Where(assignment => assignment.PrincipalId == role.Id &&
                assignment.PrincipalType == AuthorizationConstants.PrincipalTypes.Workload &&
                assignment.Status == AuthorizationConstants.LifecycleStatuses.Active &&
                (assignment.ExpiresAt == null || assignment.ExpiresAt > DateTime.UtcNow))
            .Join(_dbContext.IamPermissionSets.AsNoTracking()
                    .Where(set => set.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published),
                assignment => assignment.PermissionSetId,
                set => set.Id,
                (_, set) => set.PermissionsJson)
            .ToListAsync(context.CancellationToken);
        rolePermissions = rolePermissions
            .Concat(assignedPermissionJson.SelectMany(json => JsonSerializer.Deserialize<string[]>(json) ?? []))
            .Where(code => PermissionCatalogRules.IsValid(code, registeredPrefixes))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var requestedPermissions = (context.Request.GetParameter("requested_permissions")?.ToString() ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var permissions = WorkloadTokenExchangePolicy.IntersectPermissions(rolePermissions, requestedPermissions);
        var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Role);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, sourceSubject);
        identity.SetClaim(OpenIddictConstants.Claims.ClientId, clientId);
        identity.SetClaim(AuthorizationConstants.Claims.PrincipalType, AuthorizationConstants.PrincipalTypes.Workload);
        identity.SetClaim("act", clientId);
        identity.SetClaim("workload_role_id", role.Id.ToString("D"));
        var exchangedSessionId = Guid.NewGuid().ToString("N");
        identity.SetClaim("session_id", exchangedSessionId);
        identity.SetClaim("authorization_version", role.CreatedAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
        identity.SetClaim("permissions", string.Join(',', permissions));
        var sourceTenant = source.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(sourceTenant))
            identity.SetClaim("tenant_id", sourceTenant);
        var exchangedPolicies = await ResourcePolicyClaimBuilder.BuildAsync(
            _dbContext, role.ScopeId, [role.Key, role.Audience, clientId], context.CancellationToken);
        if (exchangedPolicies is not null)
            identity.SetClaim("resource_policies", exchangedPolicies);
        await _sessionStore.RegisterAsync(new WorkloadSessionRecord(
            exchangedSessionId, clientId, role.Id.ToString("D"),
            DateTime.UtcNow, DateTime.UtcNow.AddSeconds(Math.Clamp(role.MaxSessionSeconds, 60, 3600))),
            context.CancellationToken);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(context.Request.GetScopes().Where(scope => !scope.Contains('.', StringComparison.Ordinal)));
        principal.SetAudiences(audience);
        context.SignIn(principal);
    }

    private static bool TrustsActor(string json, string actor)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return WorkloadTokenExchangePolicy.TrustsActor(document, actor);
        }
        catch (JsonException) { return false; }
    }
}

public static class WorkloadTokenExchangePolicy
{
    public static bool TrustsActor(string json, string actor)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return TrustsActor(document, actor);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TrustsActor(JsonDocument document, string actor)
        => document.RootElement.TryGetProperty("principals", out var principals) &&
           principals.ValueKind == JsonValueKind.Array &&
           principals.EnumerateArray().Any(item => string.Equals(item.GetString(), actor, StringComparison.Ordinal));

    public static string[] IntersectPermissions(IEnumerable<string> rolePermissions, IReadOnlySet<string> requestedPermissions)
    {
        var role = rolePermissions.Where(permission => !string.IsNullOrWhiteSpace(permission)).Distinct(StringComparer.Ordinal).ToArray();
        return requestedPermissions.Count == 0 ? role : role.Where(requestedPermissions.Contains).ToArray();
    }
}

/// <summary>
/// Projects only published resource-policy statements applicable to the
/// principal into the access token. Resource services can then enforce the
/// same policy without trusting UI input or reaching Identity synchronously.
/// </summary>
internal static class ResourcePolicyClaimBuilder
{
    public static async Task<string?> BuildAsync(
        IApplicationDbContext db,
        Guid scopeId,
        IEnumerable<string> principals,
        CancellationToken cancellationToken)
    {
        var principalSet = principals
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (principalSet.Count == 0) return null;

        var policies = await db.IamResourcePolicies.AsNoTracking()
            .Where(policy => policy.ScopeId == scopeId &&
                policy.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published)
            .ToListAsync(cancellationToken);
        var statements = new List<ResourcePolicyClaim>();
        foreach (var policy in policies)
        {
            try
            {
                using var document = JsonDocument.Parse(policy.StatementsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array) continue;
                foreach (var statement in document.RootElement.EnumerateArray())
                {
                    var statementPrincipals = ReadStrings(statement, "principal");
                    if (!statementPrincipals.Any(principalSet.Contains)) continue;
                    var actions = ReadStrings(statement, "actions");
                    if (actions.Count == 0) continue;
                    statements.Add(new ResourcePolicyClaim(
                        policy.ServiceKey,
                        policy.ResourcePattern,
                        statement.TryGetProperty("effect", out var effect) && effect.ValueKind == JsonValueKind.String
                            ? effect.GetString()!.Trim().ToLowerInvariant()
                            : "allow",
                        actions,
                        statement.TryGetProperty("condition", out var condition)
                            ? condition.Clone()
                            : null));
                }
            }
            catch (JsonException)
            {
                // A malformed published policy is represented as a deny-only
                // claim so resource services fail closed for its principal.
                statements.Add(new ResourcePolicyClaim(policy.ServiceKey, policy.ResourcePattern, "deny", ["*"]));
            }
        }

        return statements.Count == 0 ? null : JsonSerializer.Serialize(statements);
    }

    private static List<string> ReadStrings(JsonElement statement, string property)
    {
        if (!statement.TryGetProperty(property, out var value)) return [];
        if (value.ValueKind == JsonValueKind.String)
            return [value.GetString() ?? string.Empty];
        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).ToList()
            : [];
    }
}

internal sealed record ResourcePolicyClaim(
    string ServiceKey,
    string ResourcePattern,
    string Effect,
    IReadOnlyList<string> Actions,
    JsonElement? Condition = null);

public class CustomValidateAuthorizationRequest :
    IOpenIddictServerHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>
{
    private readonly IConglomerateTenantRegistry _tenantRegistry;
    private readonly ILogger<CustomValidateAuthorizationRequest> _logger;

    public CustomValidateAuthorizationRequest(
        IConglomerateTenantRegistry tenantRegistry,
        ILogger<CustomValidateAuthorizationRequest> logger)
    {
        _tenantRegistry = tenantRegistry;
        _logger = logger;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateAuthorizationRequestContext>()
            .UseScopedHandler<CustomValidateAuthorizationRequest>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateAuthorizationRequestContext context)
    {
        if (!_tenantRegistry.IsEnabled || string.IsNullOrWhiteSpace(context.ClientId))
            return default;

        if (!_tenantRegistry.IsConglomerateClient(context.ClientId))
            return default;

        if (string.IsNullOrWhiteSpace(_tenantRegistry.GetClientTenant(context.ClientId)))
        {
            _logger.LogWarning("Rejecting authorization for unbound conglomerate client {ClientId}.", context.ClientId);
            context.Reject(
                OpenIddictConstants.Errors.InvalidClient,
                "The OAuth client is not bound to a tenant.",
                null);
        }

        return default;
    }
}

public class CustomPopulateTokenClaims :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly IConglomerateTenantRegistry _tenantRegistry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomPopulateTokenClaims> _logger;

    public CustomPopulateTokenClaims(
        UserManager<User> userManager,
        IApplicationDbContext dbContext,
        IConglomerateTenantRegistry tenantRegistry,
        IConfiguration configuration,
        ILogger<CustomPopulateTokenClaims> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tenantRegistry = tenantRegistry;
        _configuration = configuration;
        _logger = logger;
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleTokenRequestContext>()
            .UseScopedHandler<CustomPopulateTokenClaims>()
            .SetOrder(int.MaxValue - 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        var principal = context.Principal;
        if (principal is null) return;

        var identity = (ClaimsIdentity)principal.Identity!;

        if (string.Equals(context.Request.GrantType, AuthorizationConstants.GrantTypes.TokenExchange, StringComparison.Ordinal))
            return;

        var userId = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        // Workload principals use the OAuth client id as `sub`, while human
        // principals use the ASP.NET Identity Guid. Never pass a client id to
        // UserManager: the EF store converts ids to Guid and would throw a
        // FormatException, turning a valid client-credentials request into a
        // 500 response.
        var user = Guid.TryParse(userId, out _)
            ? await _userManager.FindByIdAsync(userId)
            : null;
        if (user is null)
        {
            if (string.Equals(context.Request.GrantType,
                    OpenIddictConstants.GrantTypes.ClientCredentials,
                    StringComparison.Ordinal))
            {
                // CustomHandleClientCredentialsRequest is the authoritative
                // workload resolver. Do not append a second role/permission
                // set when it already produced a bounded principal.
                if (identity.HasClaim("workload_role_id"))
                    return;

                var principalType = new Claim(AuthorizationConstants.Claims.PrincipalType, AuthorizationConstants.PrincipalTypes.Workload);
                principalType.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                identity.AddClaim(principalType);

                // Backward-compatible fallback for existing roles whose key is
                // the client id. New roles are resolved by audience above.
                var workloadRole = await _dbContext.IamWorkloadRoles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => (item.Key == userId || item.Audience == userId) && item.IsActive);
                if (workloadRole is not null)
                {
                    var workloadPermissions = JsonSerializer.Deserialize<string[]>(workloadRole.PermissionsJson) ?? [];
                    var boundary = await _dbContext.IamPermissionBoundaries.AsNoTracking()
                        .SingleOrDefaultAsync(item => item.IsActive && item.PrincipalType == AuthorizationConstants.PrincipalTypes.Workload &&
                            item.PrincipalId == workloadRole.Id && item.ScopeId == workloadRole.ScopeId);
                    if (boundary is not null)
                    {
                        var allowed = JsonSerializer.Deserialize<string[]>(boundary.AllowedPermissionsJson) ?? [];
                        workloadPermissions = workloadPermissions.Intersect(allowed, StringComparer.Ordinal).ToArray();
                    }
                    if (workloadPermissions.Length > 0)
                    {
                        var permissionsClaim = new Claim("permissions", string.Join(",", workloadPermissions));
                        permissionsClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                        identity.AddClaim(permissionsClaim);
                    }
                    var roleClaim = new Claim("workload_role_id", workloadRole.Id.ToString("D"));
                    roleClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                    identity.AddClaim(roleClaim);
                    var audienceClaim = new Claim("workload_audience", workloadRole.Audience);
                    audienceClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                    identity.AddClaim(audienceClaim);
                }
            }

            return;
        }

        var principalTypeClaim = new Claim(AuthorizationConstants.Claims.PrincipalType, AuthorizationConstants.PrincipalTypes.Human);
        principalTypeClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(principalTypeClaim);

        var securityVersionClaim = new Claim("securityVersion", user.SecurityStamp ?? "1");
        securityVersionClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(securityVersionClaim);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var emailClaim = new Claim(OpenIddictConstants.Claims.Email, user.Email);
            emailClaim.SetDestinations(
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(emailClaim);
        }
        identity.AddClaim(new Claim("fullName", user.FullName ?? ""));
        identity.AddClaim(new Claim("licenseNumber", user.LicenseNumber ?? ""));
        identity.AddClaim(new Claim("license_number", user.LicenseNumber ?? ""));

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            var roleClaim = new Claim(OpenIddictConstants.Claims.Role, role);
            roleClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(roleClaim);
        }

        var configuredSuperAdminIds = _configuration.GetSection("Identity:SuperAdmin:UserIds")
            .Get<string[]>() ?? [];
        if (configuredSuperAdminIds.Any(id => string.Equals(id, user.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            var superAdminClaim = new Claim("super_admin", "true");
            superAdminClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(superAdminClaim);
            if (_configuration.GetValue("Identity:SuperAdmin:RestrictToControlPlane", false))
            {
                var portalClassClaim = new Claim(PortalClassConstants.Claim, PortalClassConstants.PrivilegedOperator);
                portalClassClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                identity.AddClaim(portalClassClaim);
            }
        }

        identity.AddClaim(new Claim("scope", "hishop:permissions"));

        // The authentication cookie is the source of truth for the methods
        // actually completed. OidcLoginCompletionService adds "otp" only
        // after a successful MFA challenge. Do not infer "mfa" merely because
        // the account is enrolled: that would misrepresent an unchallenged
        // login and could weaken downstream step-up policy decisions.
        if (!identity.FindAll("amr").Any())
            identity.AddClaim(new Claim("amr", "pwd"));

        var legacyClaims = await _userManager.GetClaimsAsync(user);
        var legacyFacility = legacyClaims.FirstOrDefault(c => c.Type == "facility_id")?.Value;
        var memberships = await _dbContext.UserFacilities
            .Where(membership => membership.UserId == user.Id && membership.IsActive)
            .OrderByDescending(membership => membership.IsPrimary)
            .ThenBy(membership => membership.FacilityId)
            .Select(membership => membership.FacilityId)
            .ToListAsync();
        if (memberships.Count == 0 && !string.IsNullOrWhiteSpace(legacyFacility))
            memberships.Add(legacyFacility);
        if (memberships.Count > 0)
        {
            identity.AddClaim(new Claim("facility_id", memberships[0]));
            identity.AddClaim(new Claim("facility_ids", string.Join(",", memberships.Distinct(StringComparer.OrdinalIgnoreCase))));
        }

        var permissions = await _dbContext.RolePermissions
            .Where(rp => roles.Contains(rp.Role.Name!))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync();

        var groupIds = await _dbContext.IamGroupMemberships
            .Where(membership => membership.UserId == user.Id)
            .Select(membership => membership.GroupId)
            .ToListAsync();
        var policyScopeIds = await _dbContext.IamPermissionSetAssignments
            .Where(assignment => assignment.Status == AuthorizationConstants.LifecycleStatuses.Active &&
                (assignment.ExpiresAt == null || assignment.ExpiresAt > DateTime.UtcNow) &&
                ((assignment.PrincipalId == user.Id && assignment.PrincipalType == AuthorizationConstants.PrincipalTypes.Human) ||
                 (assignment.PrincipalType == AuthorizationConstants.PrincipalTypes.Group && groupIds.Contains(assignment.PrincipalId))))
            .Select(assignment => assignment.ScopeId)
            .Distinct()
            .ToListAsync();
        var policyClaims = new List<ResourcePolicyClaim>();
        foreach (var policyScopeId in policyScopeIds)
        {
            var policyJson = await ResourcePolicyClaimBuilder.BuildAsync(
                _dbContext, policyScopeId,
                roles.Append(user.Id.ToString("D")).Concat(groupIds.Select(id => id.ToString("D"))),
                context.CancellationToken);
            if (policyJson is not null)
                policyClaims.AddRange(JsonSerializer.Deserialize<List<ResourcePolicyClaim>>(policyJson) ?? []);
        }
        if (policyClaims.Count > 0)
        {
            var policyClaim = new Claim("resource_policies", JsonSerializer.Serialize(policyClaims));
            policyClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(policyClaim);
        }
        var assignedSetJson = await _dbContext.IamPermissionSetAssignments
            .Where(assignment => assignment.Status == AuthorizationConstants.LifecycleStatuses.Active &&
                (assignment.ExpiresAt == null || assignment.ExpiresAt > DateTime.UtcNow) &&
                ((assignment.PrincipalType == AuthorizationConstants.PrincipalTypes.Human && assignment.PrincipalId == user.Id) ||
                 (assignment.PrincipalType == "group" && groupIds.Contains(assignment.PrincipalId))))
            .Join(_dbContext.IamPermissionSets.Where(set => set.LifecycleStatus == AuthorizationConstants.LifecycleStatuses.Published),
                assignment => assignment.PermissionSetId,
                set => set.Id,
                (_, set) => set.PermissionsJson)
            .ToListAsync();
        var registeredPrefixes = await _dbContext.IamServiceDefinitions.AsNoTracking()
            .Where(item => item.IsActive).Select(item => item.PermissionPrefix)
            .ToListAsync(context.CancellationToken);
        foreach (var json in assignedSetJson)
        {
            try
            {
                permissions.AddRange((JsonSerializer.Deserialize<string[]>(json) ?? [])
                    .Where(code => PermissionCatalogRules.IsValid(code, registeredPrefixes)));
            }
            catch (JsonException)
            {
                _logger.LogWarning("Ignoring malformed IAM permission set JSON for user {UserId}.", user.Id);
            }
        }

        permissions.AddRange(await _dbContext.BreakGlassRequests
            .Where(item => item.SubjectUserId == user.Id && item.Status == "approved" &&
                item.RevokedAt == null && item.ExpiresAt > DateTime.UtcNow)
            .Select(item => item.PermissionCode)
            .ToListAsync());

        var boundaries = await _dbContext.IamPermissionBoundaries
            .Where(item => item.IsActive && item.PrincipalType == AuthorizationConstants.PrincipalTypes.Human && item.PrincipalId == user.Id)
            .ToListAsync();
        foreach (var boundary in boundaries)
        {
            try
            {
                var allowed = (JsonSerializer.Deserialize<string[]>(boundary.AllowedPermissionsJson) ?? [])
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                permissions = permissions.Where(allowed.Contains).ToList();
                if (!string.IsNullOrWhiteSpace(boundary.ResourceConstraintsJson))
                {
                    var constraints = new Claim("authorization_constraints", boundary.ResourceConstraintsJson);
                    constraints.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                    identity.AddClaim(constraints);
                }
            }
            catch (JsonException)
            {
                _logger.LogError("Denying permissions because boundary JSON is malformed for user {UserId}.", user.Id);
                permissions = [];
            }
        }

        permissions = permissions
            .Where(code => PermissionCatalogRules.IsValid(code, registeredPrefixes))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (configuredSuperAdminIds.Any(id => string.Equals(id, user.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
            permissions = PrivilegedIdentityPermissionBoundary.Filter(permissions).ToList();
        if (permissions.Count > 0)
        {
            var permissionsClaim = new Claim("permissions", string.Join(",", permissions));
            permissionsClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(permissionsClaim);
        }

        if (!await TryIssueTenantClaimsAsync(context, identity, user, context.CancellationToken))
            return;
    }

    private async Task IssueUserMembershipTenantClaimsAsync(
        ClaimsIdentity identity,
        User user,
        CancellationToken cancellationToken)
    {
        var memberships = (await _userManager.GetClaimsAsync(user))
            .Where(claim => claim.Type == "tenant_membership")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (memberships.Count == 0)
            return;

        var primaryTenant = memberships[0];
        var tenantClaim = new Claim("tenant_id", primaryTenant);
        tenantClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
        identity.AddClaim(tenantClaim);

        foreach (var membership in memberships)
        {
            var membershipClaim = new Claim("tenant_membership", membership);
            membershipClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(membershipClaim);
        }

        if (memberships.Count > 1)
        {
            var membershipsClaim = new Claim("tenant_memberships", string.Join(",", memberships));
            membershipsClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(membershipsClaim);
        }
    }

    private async Task<bool> TryIssueTenantClaimsAsync(
        OpenIddictServerEvents.HandleTokenRequestContext context,
        ClaimsIdentity identity,
        User user,
        CancellationToken cancellationToken)
    {
        if (!_tenantRegistry.IsEnabled)
            return true;

        var clientId = context.Request.ClientId;
        if (!_tenantRegistry.IsConglomerateClient(clientId))
        {
            if (_tenantRegistry.IsEnabled)
                await IssueUserMembershipTenantClaimsAsync(identity, user, cancellationToken);
            return true;
        }

        var clientTenant = _tenantRegistry.GetClientTenant(clientId);
        if (string.IsNullOrWhiteSpace(clientTenant))
        {
            _logger.LogWarning("Conglomerate client {ClientId} is missing a tenant binding.", clientId);
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "The OAuth client is not bound to a tenant.", null);
            return false;
        }

        var memberships = (await _userManager.GetClaimsAsync(user))
            .Where(claim => claim.Type == "tenant_membership")
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!memberships.Any(tenant => string.Equals(tenant, clientTenant, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning(
                "User {UserId} is not a member of tenant {TenantKey} required by client {ClientId}.",
                user.Id,
                clientTenant,
                clientId);
            context.Reject(
                OpenIddictConstants.Errors.InvalidGrant,
                "The user is not authorized for this tenant.",
                null);
            return false;
        }

        var tenantClaim = new Claim("tenant_id", clientTenant);
        tenantClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
        identity.AddClaim(tenantClaim);

        IssuePortalClaims(identity, clientId, clientTenant);

        if (memberships.Count > 1)
        {
            var membershipsClaim = new Claim("tenant_memberships", string.Join(",", memberships));
            membershipsClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(membershipsClaim);
        }

        return true;
    }

    private void IssuePortalClaims(ClaimsIdentity identity, string? clientId, string clientTenant)
    {
        var portalClass = _tenantRegistry.GetPortalClass(clientId);
        var tenantClass = _tenantRegistry.GetTenantClass(clientTenant);

        var portalClaim = new Claim(ConglomerateConstants.ClaimPortalClass, portalClass);
        portalClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(portalClaim);

        var tenantClassClaim = new Claim(ConglomerateConstants.ClaimTenantClass, tenantClass);
        tenantClassClaim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(tenantClassClaim);
    }
}
