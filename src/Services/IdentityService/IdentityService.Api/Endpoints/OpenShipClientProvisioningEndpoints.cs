using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Api.Authorization;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.Infrastructure.Audit;
using His.Hope.IdentityService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class OpenShipClientProvisioningEndpoints
{
    private const string OwnerClientIdKey = "openship.ownerClientId";
    private const string ExternalKeyKey = "openship.externalKey";
    private const string DesiredHashKey = "openship.desiredHash";
    private const string ProfileRevisionKey = "openship.profileRevision";
    private const string ServiceRevisionKey = "openship.serviceRevision";
    private const string RegistrationVersionKey = "openship.registrationVersion";
    private const string LastIdempotencyKey = "openship.lastIdempotencyKey";
    private const string CredentialVersionKey = "openship.credentialVersion";
    private const string DeliveryIdKey = "openship.credentialDeliveryId";
    private const string DeliveryRequestKey = "openship.credentialDeliveryRequestKey";
    private const string DeliveryExpiresAtKey = "openship.credentialDeliveryExpiresAt";
    private const string DeliveryAcknowledgedAtKey = "openship.credentialDeliveryAcknowledgedAt";
    private const string ActiveClientIdKey = "openship.activeClientId";
    private const string PendingClientIdKey = "openship.pendingClientId";
    private const string PendingTransitionIdKey = "openship.pendingTransitionId";
    private const string PendingTransitionKindKey = "openship.pendingTransitionKind";
    private const string PendingTransitionExpiresAtKey = "openship.pendingTransitionExpiresAt";
    private const string TransitionIdKey = "openship.transitionId";
    private const string TransitionKindKey = "openship.transitionKind";
    private const string TransitionExpiresAtKey = "openship.transitionExpiresAt";
    private const string AnchorClientIdKey = "openship.anchorClientId";
    private const string FinalizedTransitionIdKey = "openship.finalizedTransitionId";
    private const string RetiringClientIdKey = "openship.retiringClientId";
    private const string RetiredAtKey = "openship.retiredAt";
    private const string UnusedAtKey = "openship.unusedAt";
    private const string UnusedIdempotencyKey = "openship.unusedIdempotencyKey";
    private static readonly TimeSpan DeliveryWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan TransitionWindow = TimeSpan.FromHours(24);

    public static void MapOpenShipClientProvisioningEndpoints(this WebApplication app)
    {
        var environment = app.Services.GetRequiredService<IHostEnvironment>();
        var group = app.MapGroup(IdentityApiRoutes.OpenShipProvisioningClients)
            .RequireAuthorization("OpenShipManagedClients")
            .AddEndpointFilter(async (context, next) =>
            {
                if (environment.IsProduction() && !context.HttpContext.Request.IsHttps)
                    return Problem(StatusCodes.Status400BadRequest, "https_required", "Managed-client provisioning requires HTTPS.");
                return await next(context);
            });

        group.MapPut("/{externalKey}", Reconcile)
            .WithOpenApi();
        group.MapGet("/{externalKey}", ReadManagedClient)
            .WithOpenApi();
        group.MapPost("/{externalKey}/unused", MarkManagedClientUnused)
            .WithOpenApi();
        group.MapPost("/{externalKey}/credential-deliveries", CreateCredentialDelivery)
            .WithOpenApi();
        group.MapGet("/{externalKey}/credential-deliveries/{operationId}", DeliverCredential)
            .WithOpenApi();
        group.MapPost("/{externalKey}/credential-deliveries/{operationId}/ack", AcknowledgeCredentialDelivery)
            .WithOpenApi();
        group.MapPost("/{externalKey}/credential-rotations", StartCredentialRotation)
            .WithOpenApi();
        group.MapPost("/{externalKey}/transitions/{operationId}/finalize", FinalizeTransition)
            .WithOpenApi();
        group.MapPost("/{externalKey}/transitions/{operationId}/abort", AbortTransition)
            .WithOpenApi();
    }

    private static async Task<IResult> ReadManagedClient(
        string externalKey,
        IOpenIddictApplicationManager appManager,
        HttpContext http,
        CancellationToken ct)
    {
        var found = await FindOwnedClient(externalKey, appManager, http, ct);
        if (found.Error is not null) return found.Error;
        var (application, properties, clientId) = found.Value;
        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return anchor.Error;
        var anchorProperties = anchor.Value.Properties;
        var version = GetString(properties, RegistrationVersionKey);
        if (!string.IsNullOrWhiteSpace(version)) http.Response.Headers.ETag = $"\"{version}\"";
        http.Response.Headers.CacheControl = "no-store";
        object? pendingTransition = null;
        var pendingClientId = GetString(anchorProperties, PendingClientIdKey);
        if (!string.IsNullOrWhiteSpace(pendingClientId))
        {
            pendingTransition = new
            {
                clientId = pendingClientId,
                operationId = GetString(anchorProperties, PendingTransitionIdKey),
                kind = GetString(anchorProperties, PendingTransitionKindKey),
                expiresAt = GetDate(anchorProperties, PendingTransitionExpiresAtKey),
            };
        }
        return Results.Ok(new
        {
            clientId,
            clientType = await appManager.GetClientTypeAsync(application, ct),
            registrationVersion = version,
            profileRevision = GetLong(properties, ProfileRevisionKey),
            serviceRevision = GetLong(properties, ServiceRevisionKey),
            credentialVersion = GetString(properties, CredentialVersionKey),
            unused = GetDate(properties, UnusedAtKey) is not null,
            unusedAt = GetDate(properties, UnusedAtKey),
            pendingTransition,
        });
    }

    private static async Task<IResult> MarkManagedClientUnused(
        string externalKey,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var requestKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128 || !IsSafeOperationId(requestKey))
            return Problem(StatusCodes.Status400BadRequest, "idempotency_key_required", "A valid Idempotency-Key header is required.");

        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return anchor.Error;
        if (!string.IsNullOrWhiteSpace(GetString(anchor.Value.Properties, PendingClientIdKey)))
            return Problem(StatusCodes.Status409Conflict, "managed_transition_in_progress", "The managed client cannot be marked unused while a deployment transition is pending.");

        var active = await FindOwnedClient(externalKey, appManager, http, ct);
        if (active.Error is not null) return active.Error;
        var (application, properties, clientId) = active.Value;
        var currentVersion = GetString(properties, RegistrationVersionKey);
        if (!string.IsNullOrWhiteSpace(currentVersion)) http.Response.Headers.ETag = $"\"{currentVersion}\"";
        http.Response.Headers.CacheControl = "no-store";
        if (GetDate(properties, UnusedAtKey) is not null)
            return Results.Ok(new { clientId, unused = true, alreadyUnused = true, registrationVersion = currentVersion });

        var ifMatch = http.Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Problem(StatusCodes.Status428PreconditionRequired, "if_match_required", "Marking a managed client unused requires its current ETag in If-Match.");
        if (!string.Equals(ifMatch, currentVersion, StringComparison.Ordinal))
            return Problem(StatusCodes.Status409Conflict, "registration_version_conflict", "The managed client changed before it could be marked unused.");

        var updated = properties
            .SetItem(UnusedAtKey, JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow))
            .SetItem(UnusedIdempotencyKey, JsonSerializer.SerializeToElement(requestKey));
        var updateError = await UpdateProperties(application, updated, appManager, vaultStore, ct);
        if (updateError is not null) return updateError;
        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_MARK_UNUSED", "OpenShipManagedClient", clientId, ct);
        return Results.Ok(new { clientId, unused = true, registrationVersion = currentVersion });
    }

    private static async Task<IResult> Reconcile(
        string externalKey,
        ManagedClientRequest? request,
        IdentityDbContext db,
        IOpenIddictApplicationManager appManager,
        IOpenIddictScopeManager scopeManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken ct)
    {
        if (environment.IsProduction() && !http.Request.IsHttps)
            return Problem(StatusCodes.Status400BadRequest, "https_required", "Managed-client provisioning requires HTTPS.");

        var ownerClientId = CallerClientId(http);
        if (!IsValidExternalKey(externalKey) || string.IsNullOrWhiteSpace(ownerClientId) || !ExternalKeyBelongsTo(externalKey, ownerClientId))
            return Problem(StatusCodes.Status403Forbidden, "external_key_forbidden", "The external key is not owned by this workload client.");

        var idempotencyKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 128 || !IsSafeOperationId(idempotencyKey))
            return Problem(StatusCodes.Status400BadRequest, "idempotency_key_required", "A stable, valid Idempotency-Key header is required.");

        if (request is null)
            return Problem(StatusCodes.Status400BadRequest, "invalid_registration", "A managed-client registration body is required.");
        if (!TryNormalize(request, out var normalized, out var validationError))
            return Problem(StatusCodes.Status400BadRequest, "invalid_registration", validationError!);

        foreach (var redirect in normalized.RedirectUris.Concat(normalized.PostLogoutRedirectUris))
        {
            var uri = new Uri(redirect, UriKind.Absolute);
            try
            {
                var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct);
                if (addresses.Length == 0 || addresses.Any(address => IsPrivateAddress(address.ToString())))
                    return Problem(StatusCodes.Status400BadRequest, "unsafe_redirect_uri", "Redirect URI hosts must resolve only to public IP addresses.");
            }
            catch (SocketException)
            {
                return Problem(StatusCodes.Status400BadRequest, "redirect_host_unresolvable", "Redirect URI hosts must resolve before registration.");
            }
        }

        foreach (var scope in normalized.Scopes.Where(scope => scope is not "openid" and not "profile" and not "email" and not "address" and not "phone" and not "offline_access"))
        {
            if (await scopeManager.FindByNameAsync(scope, ct) is null)
                return Problem(StatusCodes.Status400BadRequest, "unknown_scope", $"The requested scope '{scope}' is not registered by IdentityService.");
        }

        var anchorClientId = ManagedClientId(externalKey);
        var anchorObject = await appManager.FindByClientIdAsync(anchorClientId, ct);
        var desiredHash = Hash(JsonSerializer.Serialize(normalized));
        if (anchorObject is null)
        {
            if (normalized.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
                environment.IsProduction() && !vaultStore.UsesPersistentStore)
            {
                return Problem(StatusCodes.Status503ServiceUnavailable, "credential_store_unavailable", "IdentityService requires its persistent Vault credential store for confidential managed clients.");
            }

            return await CreateManagedClient(
                externalKey, ownerClientId, anchorClientId, idempotencyKey, normalized, desiredHash,
                db, appManager, vaultStore, audit, http, ct);
        }

        var anchorProperties = await appManager.GetPropertiesAsync(anchorObject, ct);
        if (!Owns(anchorProperties, ownerClientId, externalKey))
            return Problem(StatusCodes.Status409Conflict, "managed_client_ownership_conflict", "The client ID is already associated with a different owner.");

        var pendingClientId = GetString(anchorProperties, PendingClientIdKey);
        var pendingTransitionId = GetString(anchorProperties, PendingTransitionIdKey);
        var pendingExpiresAt = GetDate(anchorProperties, PendingTransitionExpiresAtKey);
        if (!string.IsNullOrWhiteSpace(pendingClientId) && !string.IsNullOrWhiteSpace(pendingTransitionId))
        {
            if (pendingExpiresAt is null || pendingExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                var expiryError = await ExpirePendingTransition(anchorObject, anchorProperties, pendingClientId,
                    pendingTransitionId, appManager, vaultStore, ct);
                if (expiryError is not null) return expiryError;
                anchorProperties = await appManager.GetPropertiesAsync(anchorObject, ct);
            }
            else
            {
                var pendingObject = await appManager.FindByClientIdAsync(pendingClientId, ct);
                if (pendingObject is null)
                    return Problem(StatusCodes.Status409Conflict, "managed_transition_unavailable", "The pending managed-client transition is unavailable and must be aborted before retrying.");
                var pendingProperties = await appManager.GetPropertiesAsync(pendingObject, ct);
                if (!Owns(pendingProperties, ownerClientId, externalKey))
                    return Problem(StatusCodes.Status409Conflict, "managed_client_ownership_conflict", "The pending client is not owned by this OpenShip workload.");
                if (string.Equals(GetString(pendingProperties, DesiredHashKey), desiredHash, StringComparison.Ordinal) &&
                    GetLong(pendingProperties, ProfileRevisionKey) == normalized.ProfileRevision &&
                    GetLong(pendingProperties, ServiceRevisionKey) == normalized.ServiceRevision)
                {
                    return ManagedClientResponse(http, externalKey, pendingClientId,
                        GetString(pendingProperties, RegistrationVersionKey),
                        GetString(pendingProperties, CredentialVersionKey), created: false,
                        transitionId: pendingTransitionId,
                        transitionKind: GetString(pendingProperties, TransitionKindKey),
                        transitionExpiresAt: pendingExpiresAt,
                        activeClientId: GetString(anchorProperties, ActiveClientIdKey) ?? anchorClientId);
                }
                return Problem(StatusCodes.Status409Conflict, "managed_transition_in_progress", "A different managed-client transition is still pending. Finalize it after a successful deployment or abort it before changing the desired client state.");
            }
        }

        var clientId = GetString(anchorProperties, ActiveClientIdKey) ?? anchorClientId;
        var existingObject = await appManager.FindByClientIdAsync(clientId, ct);
        if (existingObject is null)
            return Problem(StatusCodes.Status409Conflict, "managed_client_unavailable", "IdentityService could not load the active managed client.");
        var application = await db.OpenIddictApplications
            .FirstOrDefaultAsync(candidate => candidate.ClientId == clientId, ct);
        if (application is null)
            return Problem(StatusCodes.Status409Conflict, "managed_client_unavailable", "IdentityService could not load the managed client record.");

        var properties = await appManager.GetPropertiesAsync(existingObject, ct);
        if (!Owns(properties, ownerClientId, externalKey))
            return Problem(StatusCodes.Status409Conflict, "managed_client_ownership_conflict", "The client ID is already associated with a different owner.");

        var oldHash = GetString(properties, DesiredHashKey);
        var oldIdempotencyKey = GetString(properties, LastIdempotencyKey);
        if (string.Equals(oldIdempotencyKey, idempotencyKey, StringComparison.Ordinal) &&
            (!string.Equals(oldHash, desiredHash, StringComparison.Ordinal) ||
             normalized.ProfileRevision != GetLong(properties, ProfileRevisionKey) ||
             normalized.ServiceRevision != GetLong(properties, ServiceRevisionKey)))
        {
            return Problem(StatusCodes.Status409Conflict, "idempotency_key_reused", "The Idempotency-Key was already used for a different client state.");
        }

        var oldProfileRevision = GetLong(properties, ProfileRevisionKey);
        var oldServiceRevision = GetLong(properties, ServiceRevisionKey);
        if (normalized.ProfileRevision < oldProfileRevision || normalized.ServiceRevision < oldServiceRevision)
            return Problem(StatusCodes.Status409Conflict, "stale_desired_state", "The requested client state is older than the last reconciled state.");

        var actualClientType = await appManager.GetClientTypeAsync(existingObject, ct);

        var oldVersion = GetString(properties, RegistrationVersionKey);
        if (string.Equals(oldHash, desiredHash, StringComparison.Ordinal) &&
            normalized.ProfileRevision == oldProfileRevision &&
            normalized.ServiceRevision == oldServiceRevision)
        {
            return ManagedClientResponse(http, externalKey, clientId, oldVersion, GetString(properties, CredentialVersionKey), created: false);
        }

        if (normalized.ProfileRevision == oldProfileRevision && normalized.ServiceRevision == oldServiceRevision)
            return Problem(StatusCodes.Status409Conflict, "revision_state_conflict", "The same profile and service revisions cannot describe different client settings.");

        var ifMatch = http.Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Problem(StatusCodes.Status428PreconditionRequired, "if_match_required", "Updating an existing managed client requires its current ETag in If-Match.");
        if (!string.Equals(ifMatch, oldVersion, StringComparison.Ordinal))
            return Problem(StatusCodes.Status409Conflict, "registration_version_conflict", "The managed client changed since OpenShip last read it.");

        if (normalized.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
            environment.IsProduction() && !vaultStore.UsesPersistentStore)
        {
            return Problem(StatusCodes.Status503ServiceUnavailable, "credential_store_unavailable", "IdentityService requires its persistent Vault credential store for confidential managed clients.");
        }

        // Keep the active registration unchanged while the next workload is
        // being built and rolled out. The candidate becomes active only after
        // OpenShip confirms the new K3s deployment is Ready.
        var transitionKind = string.Equals(actualClientType, normalized.ClientType, StringComparison.Ordinal)
            ? "registration-update"
            : "client-type-migration";
        return await StageManagedClientTransition(
            anchorObject, anchorProperties, existingObject, properties, clientId, externalKey, ownerClientId,
            idempotencyKey, normalized, desiredHash, transitionKind, appManager,
            vaultStore, audit, http, ct);
    }

    private static async Task<IResult> StageManagedClientTransition(
        object anchorApplication,
        ImmutableDictionary<string, JsonElement> anchorProperties,
        object currentApplication,
        ImmutableDictionary<string, JsonElement> currentProperties,
        string currentClientId,
        string externalKey,
        string ownerClientId,
        string idempotencyKey,
        NormalizedManagedClient desired,
        string desiredHash,
        string transitionKind,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var currentVersion = GetString(currentProperties, RegistrationVersionKey) ?? string.Empty;
        var transitionId = $"tr-{Hash($"{externalKey}:{currentVersion}:{desiredHash}:{transitionKind}")[..32]}";
        var candidateClientId = $"{ManagedClientId(externalKey)}-g{Hash(transitionId)[..12]}";
        var candidate = await appManager.FindByClientIdAsync(candidateClientId, ct);
        var candidateProperties = candidate is null
            ? null
            : await appManager.GetPropertiesAsync(candidate, ct);
        if (candidateProperties is not null &&
            (!Owns(candidateProperties, ownerClientId, externalKey) ||
             !string.Equals(GetString(candidateProperties, TransitionIdKey), transitionId, StringComparison.Ordinal)))
        {
            return Problem(StatusCodes.Status409Conflict, "managed_transition_collision", "The transition candidate is associated with a different owner or operation.");
        }

        var registrationVersion = GetString(candidateProperties ?? ImmutableDictionary<string, JsonElement>.Empty, RegistrationVersionKey)
            ?? Guid.NewGuid().ToString("N");
        var candidateRetired = candidateProperties is not null && GetDate(candidateProperties, RetiredAtKey) is not null;
        var existingCandidateCredentialVersion = GetString(candidateProperties ?? ImmutableDictionary<string, JsonElement>.Empty, CredentialVersionKey);
        var candidateSecret = candidate is null || candidateRetired || desired.ClientType != OpenIddictConstants.ClientTypes.Confidential
            ? null
            : await vaultStore.GetSecretAsync(candidateClientId, ct);
        var credentialNeedsRepair = desired.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
            (candidateRetired || string.IsNullOrWhiteSpace(candidateSecret));
        var credentialVersion = desired.ClientType == OpenIddictConstants.ClientTypes.Confidential
            ? credentialNeedsRepair || existingCandidateCredentialVersion is null
                ? Guid.NewGuid().ToString("N")
                : existingCandidateCredentialVersion
            : null;
        // An expired, retired candidate is retried as a fresh bounded
        // transition. Reusing its expired timestamp would stage a candidate
        // that could never be finalized.
        var transitionExpiresAt = !candidateRetired
            ? GetDate(candidateProperties ?? ImmutableDictionary<string, JsonElement>.Empty, TransitionExpiresAtKey)
            : null;
        transitionExpiresAt ??= DateTimeOffset.UtcNow.Add(TransitionWindow);
        var secret = credentialVersion is null
            ? null
            : candidateSecret ?? vaultStore.GenerateSecret(candidateClientId);
        var nextProperties = currentProperties
            .Remove(ActiveClientIdKey)
            .Remove(PendingClientIdKey)
            .Remove(PendingTransitionIdKey)
            .Remove(PendingTransitionKindKey)
            .Remove(PendingTransitionExpiresAtKey)
            .Remove(DeliveryIdKey)
            .Remove(DeliveryRequestKey)
            .Remove(DeliveryExpiresAtKey)
            .Remove(DeliveryAcknowledgedAtKey)
            .Remove(UnusedAtKey)
            .Remove(UnusedIdempotencyKey)
            .SetItem(OwnerClientIdKey, JsonSerializer.SerializeToElement(ownerClientId))
            .SetItem(ExternalKeyKey, JsonSerializer.SerializeToElement(externalKey))
            .SetItem(DesiredHashKey, JsonSerializer.SerializeToElement(desiredHash))
            .SetItem(ProfileRevisionKey, JsonSerializer.SerializeToElement(desired.ProfileRevision))
            .SetItem(ServiceRevisionKey, JsonSerializer.SerializeToElement(desired.ServiceRevision))
            .SetItem(RegistrationVersionKey, JsonSerializer.SerializeToElement(registrationVersion))
            .SetItem(LastIdempotencyKey, JsonSerializer.SerializeToElement(idempotencyKey))
            .SetItem(TransitionIdKey, JsonSerializer.SerializeToElement(transitionId))
            .SetItem(TransitionKindKey, JsonSerializer.SerializeToElement(transitionKind))
            .SetItem(TransitionExpiresAtKey, JsonSerializer.SerializeToElement(transitionExpiresAt))
            .SetItem(AnchorClientIdKey, JsonSerializer.SerializeToElement(ManagedClientId(externalKey)));
        if (credentialVersion is not null)
            nextProperties = nextProperties.SetItem(CredentialVersionKey, JsonSerializer.SerializeToElement(credentialVersion));
        else
            nextProperties = nextProperties.Remove(CredentialVersionKey);

        if (candidate is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await appManager.PopulateAsync(descriptor, currentApplication, ct);
            descriptor.ClientId = candidateClientId;
            descriptor.DisplayName = desired.DisplayName;
            descriptor.ClientType = desired.ClientType;
            descriptor.ClientSecret = secret;
            descriptor.Permissions.Clear();
            descriptor.Permissions.UnionWith(desired.Permissions);
            descriptor.RedirectUris.Clear();
            descriptor.RedirectUris.UnionWith(desired.RedirectUris.Select(value => new Uri(value, UriKind.Absolute)));
            descriptor.PostLogoutRedirectUris.Clear();
            descriptor.PostLogoutRedirectUris.UnionWith(desired.PostLogoutRedirectUris.Select(value => new Uri(value, UriKind.Absolute)));
            descriptor.Requirements.Clear();
            if (desired.ClientType == OpenIddictConstants.ClientTypes.Public)
                descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
            SetProperties(descriptor, nextProperties);
            try
            {
                await appManager.CreateAsync(descriptor, ct);
                if (secret is not null) await vaultStore.StoreSecretAsync(candidateClientId, secret, ct);
            }
            catch (DbUpdateException)
            {
                var raced = await appManager.FindByClientIdAsync(candidateClientId, ct);
                if (raced is null) throw;
                var racedProperties = await appManager.GetPropertiesAsync(raced, ct);
                if (!Owns(racedProperties, ownerClientId, externalKey) ||
                    !string.Equals(GetString(racedProperties, TransitionIdKey), transitionId, StringComparison.Ordinal))
                    return Problem(StatusCodes.Status409Conflict, "managed_transition_collision", "A different managed-client transition created the candidate concurrently.");
                candidate = raced;
                candidateProperties = racedProperties;
            }
        }
        else
        {
            // Reuse the same candidate for idempotent retries. A previous
            // attempt may have retired it after an anchor concurrency conflict,
            // or may have created the application before Vault persisted its
            // secret. Repair only this exact owned transition candidate.
            var existingCandidateSecret = candidateRetired || credentialVersion is null
                ? null
                : candidateSecret;
            var needsRepair = candidateRetired ||
                (credentialVersion is not null && (
                    string.IsNullOrWhiteSpace(existingCandidateSecret) ||
                    !string.Equals(existingCandidateCredentialVersion, credentialVersion, StringComparison.Ordinal)
                ));
            if (needsRepair)
            {
                var repairedSecret = credentialVersion is null
                    ? null
                    : candidateRetired || string.IsNullOrWhiteSpace(existingCandidateSecret)
                        ? secret
                        : existingCandidateSecret;
                nextProperties = nextProperties.Remove(RetiredAtKey);
                var descriptor = new OpenIddictApplicationDescriptor();
                await appManager.PopulateAsync(descriptor, currentApplication, ct);
                descriptor.ClientId = candidateClientId;
                descriptor.DisplayName = desired.DisplayName;
                descriptor.ClientType = desired.ClientType;
                descriptor.ClientSecret = repairedSecret;
                descriptor.Permissions.Clear();
                descriptor.Permissions.UnionWith(desired.Permissions);
                descriptor.RedirectUris.Clear();
                descriptor.RedirectUris.UnionWith(desired.RedirectUris.Select(value => new Uri(value, UriKind.Absolute)));
                descriptor.PostLogoutRedirectUris.Clear();
                descriptor.PostLogoutRedirectUris.UnionWith(desired.PostLogoutRedirectUris.Select(value => new Uri(value, UriKind.Absolute)));
                descriptor.Requirements.Clear();
                if (desired.ClientType == OpenIddictConstants.ClientTypes.Public)
                    descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
                SetProperties(descriptor, nextProperties);
                try
                {
                    if (repairedSecret is not null && string.IsNullOrWhiteSpace(await vaultStore.GetSecretAsync(candidateClientId, ct)))
                        await vaultStore.StoreSecretAsync(candidateClientId, repairedSecret, ct);
                    await appManager.UpdateAsync(candidate, descriptor, ct);
                }
                catch
                {
                    await DeactivateManagedClient(candidateClientId, appManager, vaultStore, ct);
                    throw;
                }
                candidateProperties = nextProperties;
            }
            else
            {
                // A healthy candidate already contains the exact staged
                // registration and credential; keep its original version.
                nextProperties = candidateProperties!;
            }
        }

        var anchorUpdated = anchorProperties
            .SetItem(PendingClientIdKey, JsonSerializer.SerializeToElement(candidateClientId))
            .SetItem(PendingTransitionIdKey, JsonSerializer.SerializeToElement(transitionId))
            .SetItem(PendingTransitionKindKey, JsonSerializer.SerializeToElement(transitionKind))
            .SetItem(PendingTransitionExpiresAtKey, JsonSerializer.SerializeToElement(transitionExpiresAt));
        var updateError = await UpdateProperties(anchorApplication, anchorUpdated, appManager, vaultStore, ct);
        if (updateError is not null)
        {
            var currentAnchorProperties = await appManager.GetPropertiesAsync(anchorApplication, ct);
            if (string.Equals(GetString(currentAnchorProperties, PendingTransitionIdKey), transitionId, StringComparison.Ordinal) &&
                string.Equals(GetString(currentAnchorProperties, PendingClientIdKey), candidateClientId, StringComparison.Ordinal))
            {
                return ManagedClientResponse(http, externalKey, candidateClientId, registrationVersion, credentialVersion,
                    created: false,
                    transitionId: transitionId,
                    transitionKind: transitionKind,
                    transitionExpiresAt: transitionExpiresAt,
                    activeClientId: currentClientId);
            }
            return updateError;
        }

        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_TRANSITION_STAGE", "OpenShipManagedClient", candidateClientId, ct);
        return ManagedClientResponse(http, externalKey, candidateClientId, registrationVersion, credentialVersion,
            created: candidateProperties is null,
            transitionId: transitionId,
            transitionKind: transitionKind,
            transitionExpiresAt: transitionExpiresAt,
            activeClientId: currentClientId);
    }

    private static async Task<IResult> CreateManagedClient(
        string externalKey,
        string ownerClientId,
        string clientId,
        string idempotencyKey,
        NormalizedManagedClient request,
        string desiredHash,
        IdentityDbContext db,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var registrationVersion = Guid.NewGuid().ToString("N");
        var credentialVersion = request.ClientType == OpenIddictConstants.ClientTypes.Confidential
            ? Guid.NewGuid().ToString("N")
            : null;
        var secret = credentialVersion is null ? null : vaultStore.GenerateSecret(clientId);
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [OwnerClientIdKey] = JsonSerializer.SerializeToElement(ownerClientId),
            [ExternalKeyKey] = JsonSerializer.SerializeToElement(externalKey),
            [ActiveClientIdKey] = JsonSerializer.SerializeToElement(clientId),
            [DesiredHashKey] = JsonSerializer.SerializeToElement(desiredHash),
            [ProfileRevisionKey] = JsonSerializer.SerializeToElement(request.ProfileRevision),
            [ServiceRevisionKey] = JsonSerializer.SerializeToElement(request.ServiceRevision),
            [RegistrationVersionKey] = JsonSerializer.SerializeToElement(registrationVersion),
            [LastIdempotencyKey] = JsonSerializer.SerializeToElement(idempotencyKey),
        };
        if (credentialVersion is not null)
            properties[CredentialVersionKey] = JsonSerializer.SerializeToElement(credentialVersion);

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = request.DisplayName,
            ClientType = request.ClientType,
            ClientSecret = secret,
        };
        SetProperties(descriptor, properties.ToImmutableDictionary(StringComparer.Ordinal));
        descriptor.Permissions.UnionWith(request.Permissions);
        descriptor.RedirectUris.UnionWith(request.RedirectUris.Select(value => new Uri(value, UriKind.Absolute)));
        descriptor.PostLogoutRedirectUris.UnionWith(request.PostLogoutRedirectUris.Select(value => new Uri(value, UriKind.Absolute)));
        if (request.ClientType == OpenIddictConstants.ClientTypes.Public)
            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);

        var created = false;
        try
        {
            await appManager.CreateAsync(descriptor, ct);
            created = true;
            if (secret is not null)
                await vaultStore.StoreSecretAsync(clientId, secret, ct);
        }
        catch (DbUpdateException) when (!created)
        {
            var racedObject = await appManager.FindByClientIdAsync(clientId, ct);
            if (racedObject is not null)
            {
                var racedProperties = await appManager.GetPropertiesAsync(racedObject, ct);
                if (Owns(racedProperties, ownerClientId, externalKey) &&
                    string.Equals(GetString(racedProperties, DesiredHashKey), desiredHash, StringComparison.Ordinal) &&
                    GetLong(racedProperties, ProfileRevisionKey) == request.ProfileRevision &&
                    GetLong(racedProperties, ServiceRevisionKey) == request.ServiceRevision)
                {
                    return ManagedClientResponse(
                        http,
                        externalKey,
                        clientId,
                        GetString(racedProperties, RegistrationVersionKey),
                        GetString(racedProperties, CredentialVersionKey),
                        created: false);
                }
            }
            return Problem(StatusCodes.Status409Conflict, "managed_client_create_conflict", "A different managed-client state was created concurrently.");
        }
        catch
        {
            if (created)
            {
                var createdObject = await appManager.FindByClientIdAsync(clientId, ct);
                if (createdObject is not null) await appManager.DeleteAsync(createdObject, ct);
                if (secret is not null) await vaultStore.RevokeSecretAsync(clientId, ct);
            }
            throw;
        }

        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_CREATE", "OpenShipManagedClient", clientId, ct);
        var createdApplication = await db.OpenIddictApplications.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ClientId == clientId, ct);
        if (createdApplication is null)
            return Problem(StatusCodes.Status503ServiceUnavailable, "managed_client_unavailable", "IdentityService could not reload the created client.");
        return ManagedClientResponse(http, externalKey, clientId, registrationVersion, credentialVersion, created: true);
    }

    private static async Task<IResult> StartCredentialRotation(
        string externalKey,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        IHostEnvironment environment,
        CancellationToken ct)
    {
        if (environment.IsProduction() && !vaultStore.UsesPersistentStore)
            return Problem(StatusCodes.Status503ServiceUnavailable, "credential_store_unavailable", "IdentityService requires its persistent Vault credential store for confidential managed clients.");

        var requestKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128 || !IsSafeOperationId(requestKey))
            return Problem(StatusCodes.Status400BadRequest, "idempotency_key_required", "A valid Idempotency-Key header is required.");

        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return anchor.Error;
        var active = await FindOwnedClient(externalKey, appManager, http, ct);
        if (active.Error is not null) return active.Error;
        var (activeApplication, activeProperties, activeClientId) = active.Value;
        if (await appManager.GetClientTypeAsync(activeApplication, ct) != OpenIddictConstants.ClientTypes.Confidential)
            return Problem(StatusCodes.Status409Conflict, "public_client_has_no_secret", "Credential rotation is supported only for confidential managed clients.");

        var anchorProperties = anchor.Value.Properties;
        var pendingClientId = GetString(anchorProperties, PendingClientIdKey);
        var pendingOperationId = GetString(anchorProperties, PendingTransitionIdKey);
        if (!string.IsNullOrWhiteSpace(pendingClientId) && !string.IsNullOrWhiteSpace(pendingOperationId))
        {
            var pending = await appManager.FindByClientIdAsync(pendingClientId, ct);
            if (pending is not null)
            {
                var pendingProperties = await appManager.GetPropertiesAsync(pending, ct);
                if (string.Equals(GetString(pendingProperties, TransitionKindKey), "credential-rotation", StringComparison.Ordinal) &&
                    string.Equals(GetString(pendingProperties, LastIdempotencyKey), requestKey, StringComparison.Ordinal))
                {
                    return ManagedClientResponse(http, externalKey, pendingClientId,
                        GetString(pendingProperties, RegistrationVersionKey),
                        GetString(pendingProperties, CredentialVersionKey), created: false,
                        transitionId: pendingOperationId, transitionKind: "credential-rotation",
                        transitionExpiresAt: GetDate(pendingProperties, TransitionExpiresAtKey),
                        activeClientId: activeClientId);
                }
            }
            return Problem(StatusCodes.Status409Conflict, "managed_transition_in_progress", "A managed-client transition is already pending.");
        }

        var currentVersion = GetString(activeProperties, RegistrationVersionKey);
        var ifMatch = http.Request.Headers.IfMatch.FirstOrDefault()?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Problem(StatusCodes.Status428PreconditionRequired, "if_match_required", "Credential rotation requires the current managed-client ETag in If-Match.");
        if (!string.Equals(ifMatch, currentVersion, StringComparison.Ordinal))
            return Problem(StatusCodes.Status409Conflict, "registration_version_conflict", "The managed client changed before credential rotation.");

        var transitionId = $"tr-{Hash($"{externalKey}:{currentVersion}:{GetString(activeProperties, CredentialVersionKey)}:{requestKey}:credential-rotation")[..32]}";
        var candidateClientId = $"{ManagedClientId(externalKey)}-g{Hash(transitionId)[..12]}";
        var candidateExisting = await appManager.FindByClientIdAsync(candidateClientId, ct);
        var candidateProperties = candidateExisting is null
            ? null
            : await appManager.GetPropertiesAsync(candidateExisting, ct);
        if (candidateProperties is not null &&
            (!Owns(candidateProperties, CallerClientId(http)!, externalKey) ||
             !string.Equals(GetString(candidateProperties, TransitionIdKey), transitionId, StringComparison.Ordinal) ||
             !string.Equals(GetString(candidateProperties, LastIdempotencyKey), requestKey, StringComparison.Ordinal) ||
             !string.Equals(GetString(candidateProperties, TransitionKindKey), "credential-rotation", StringComparison.Ordinal)))
        {
            return Problem(StatusCodes.Status409Conflict, "managed_transition_collision", "A credential-rotation candidate is associated with a different owner or operation.");
        }

        var existingCredentialVersion = GetString(candidateProperties ?? ImmutableDictionary<string, JsonElement>.Empty, CredentialVersionKey);
        var registrationVersion = GetString(candidateProperties ?? ImmutableDictionary<string, JsonElement>.Empty, RegistrationVersionKey)
            ?? Guid.NewGuid().ToString("N");
        var expiresAt = GetDate(candidateProperties ?? ImmutableDictionary<string, JsonElement>.Empty, TransitionExpiresAtKey)
            ?? DateTimeOffset.UtcNow.Add(TransitionWindow);
        if (expiresAt <= DateTimeOffset.UtcNow)
            return Problem(StatusCodes.Status410Gone, "managed_transition_expired", "The previous credential-rotation attempt expired; start a new rotation with a fresh idempotency key.");
        var candidateRetired = candidateProperties is not null && GetDate(candidateProperties, RetiredAtKey) is not null;
        var secret = candidateExisting is null || candidateRetired
            ? null
            : await vaultStore.GetSecretAsync(candidateClientId, ct);
        var needsRepair = candidateExisting is null || candidateRetired ||
            existingCredentialVersion is null || string.IsNullOrWhiteSpace(secret);
        var credentialVersion = needsRepair || existingCredentialVersion is null
            ? Guid.NewGuid().ToString("N")
            : existingCredentialVersion;
        if (needsRepair && string.IsNullOrWhiteSpace(secret))
            secret = vaultStore.GenerateSecret(candidateClientId);
        var nextProperties = activeProperties
            .Remove(ActiveClientIdKey)
            .Remove(PendingClientIdKey)
            .Remove(PendingTransitionIdKey)
            .Remove(PendingTransitionKindKey)
            .Remove(PendingTransitionExpiresAtKey)
            .Remove(DeliveryIdKey)
            .Remove(DeliveryRequestKey)
            .Remove(DeliveryExpiresAtKey)
            .Remove(DeliveryAcknowledgedAtKey)
            .Remove(UnusedAtKey)
            .Remove(UnusedIdempotencyKey)
            .SetItem(RegistrationVersionKey, JsonSerializer.SerializeToElement(registrationVersion))
            .SetItem(CredentialVersionKey, JsonSerializer.SerializeToElement(credentialVersion))
            .SetItem(LastIdempotencyKey, JsonSerializer.SerializeToElement(requestKey))
            .SetItem(TransitionIdKey, JsonSerializer.SerializeToElement(transitionId))
            .SetItem(TransitionKindKey, JsonSerializer.SerializeToElement("credential-rotation"))
            .SetItem(TransitionExpiresAtKey, JsonSerializer.SerializeToElement(expiresAt))
            .Remove(RetiredAtKey)
            .SetItem(AnchorClientIdKey, JsonSerializer.SerializeToElement(anchor.Value.ClientId));

        if (needsRepair)
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await appManager.PopulateAsync(descriptor, activeApplication, ct);
            descriptor.ClientId = candidateClientId;
            descriptor.ClientSecret = secret;
            SetProperties(descriptor, nextProperties);
            try
            {
                if (candidateExisting is null)
                    await appManager.CreateAsync(descriptor, ct);
                else
                    await appManager.UpdateAsync(candidateExisting, descriptor, ct);
                if (secret is not null && string.IsNullOrWhiteSpace(await vaultStore.GetSecretAsync(candidateClientId, ct)))
                    await vaultStore.StoreSecretAsync(candidateClientId, secret, ct);
            }
            catch
            {
                var partial = await appManager.FindByClientIdAsync(candidateClientId, ct);
                if (partial is not null) await DeactivateManagedClient(candidateClientId, appManager, vaultStore, ct);
                throw;
            }
        }

        var anchorUpdated = anchorProperties
            .SetItem(PendingClientIdKey, JsonSerializer.SerializeToElement(candidateClientId))
            .SetItem(PendingTransitionIdKey, JsonSerializer.SerializeToElement(transitionId))
            .SetItem(PendingTransitionKindKey, JsonSerializer.SerializeToElement("credential-rotation"))
            .SetItem(PendingTransitionExpiresAtKey, JsonSerializer.SerializeToElement(expiresAt));
        var updateError = await UpdateProperties(anchor.Value.Application, anchorUpdated, appManager, vaultStore, ct);
        if (updateError is not null)
        {
            var currentAnchorProperties = await appManager.GetPropertiesAsync(anchor.Value.Application, ct);
            if (string.Equals(GetString(currentAnchorProperties, PendingTransitionIdKey), transitionId, StringComparison.Ordinal) &&
                string.Equals(GetString(currentAnchorProperties, PendingClientIdKey), candidateClientId, StringComparison.Ordinal))
            {
                return ManagedClientResponse(http, externalKey, candidateClientId, registrationVersion, credentialVersion,
                    created: false,
                    transitionId: transitionId,
                    transitionKind: "credential-rotation",
                    transitionExpiresAt: expiresAt,
                    activeClientId: activeClientId);
            }
            var staged = await appManager.FindByClientIdAsync(candidateClientId, ct);
            if (staged is not null) await DeactivateManagedClient(candidateClientId, appManager, vaultStore, ct);
            return updateError;
        }

        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_CREDENTIAL_ROTATION_STAGE", "OpenShipManagedClient", candidateClientId, ct);
        return ManagedClientResponse(http, externalKey, candidateClientId, registrationVersion, credentialVersion,
            created: true,
            transitionId: transitionId,
            transitionKind: "credential-rotation",
            transitionExpiresAt: expiresAt,
            activeClientId: activeClientId);
    }

    private static async Task<IResult> FinalizeTransition(
        string externalKey,
        string operationId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return anchor.Error;
        var anchorApplication = anchor.Value.Application;
        var anchorProperties = anchor.Value.Properties;
        var pendingOperationId = GetString(anchorProperties, PendingTransitionIdKey);
        var pendingClientId = GetString(anchorProperties, PendingClientIdKey);
        if (string.IsNullOrWhiteSpace(pendingOperationId) || string.IsNullOrWhiteSpace(pendingClientId))
        {
            if (string.Equals(GetString(anchorProperties, FinalizedTransitionIdKey), operationId, StringComparison.Ordinal))
            {
                var retiringId = GetString(anchorProperties, RetiringClientIdKey);
                if (!string.IsNullOrWhiteSpace(retiringId))
                    await DeactivateManagedClient(retiringId, appManager, vaultStore, ct);
                http.Response.Headers.CacheControl = "no-store";
                return Results.Ok(new { finalized = true, alreadyFinalized = true });
            }
            return Problem(StatusCodes.Status404NotFound, "managed_transition_not_found", "The managed-client transition was not found.");
        }
        if (!string.Equals(pendingOperationId, operationId, StringComparison.Ordinal))
            return Problem(StatusCodes.Status409Conflict, "managed_transition_mismatch", "The requested transition is not the current pending operation.");
        var expiresAt = GetDate(anchorProperties, PendingTransitionExpiresAtKey);
        if (expiresAt is null || expiresAt.Value <= DateTimeOffset.UtcNow)
            return Problem(StatusCodes.Status410Gone, "managed_transition_expired", "The managed-client transition expired before it was finalized.");

        var candidate = await appManager.FindByClientIdAsync(pendingClientId, ct);
        if (candidate is null)
            return Problem(StatusCodes.Status409Conflict, "managed_transition_unavailable", "The staged managed client is unavailable.");
        var candidateProperties = await appManager.GetPropertiesAsync(candidate, ct);
        var ownerClientId = CallerClientId(http)!;
        if (!Owns(candidateProperties, ownerClientId, externalKey) ||
            !string.Equals(GetString(candidateProperties, TransitionIdKey), operationId, StringComparison.Ordinal))
            return Problem(StatusCodes.Status409Conflict, "managed_transition_ownership_conflict", "The staged client does not match this owned transition.");

        var oldClientId = GetString(anchorProperties, ActiveClientIdKey) ?? anchor.Value.ClientId;
        var promoted = anchorProperties
            .SetItem(ActiveClientIdKey, JsonSerializer.SerializeToElement(pendingClientId))
            .SetItem(FinalizedTransitionIdKey, JsonSerializer.SerializeToElement(operationId))
            .SetItem(RetiringClientIdKey, JsonSerializer.SerializeToElement(oldClientId))
            .Remove(PendingClientIdKey)
            .Remove(PendingTransitionIdKey)
            .Remove(PendingTransitionKindKey)
            .Remove(PendingTransitionExpiresAtKey);
        var updateError = await UpdateProperties(anchorApplication, promoted, appManager, vaultStore, ct);
        if (updateError is not null) return updateError;
        await DeactivateManagedClient(oldClientId, appManager, vaultStore, ct);
        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_TRANSITION_FINALIZE", "OpenShipManagedClient", pendingClientId, ct);
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { finalized = true, clientId = pendingClientId });
    }

    private static async Task<IResult> AbortTransition(
        string externalKey,
        string operationId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        IAuditService audit,
        HttpContext http,
        CancellationToken ct)
    {
        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return anchor.Error;
        var pendingOperationId = GetString(anchor.Value.Properties, PendingTransitionIdKey);
        var pendingClientId = GetString(anchor.Value.Properties, PendingClientIdKey);
        if (!string.Equals(pendingOperationId, operationId, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(pendingClientId))
            return Problem(StatusCodes.Status404NotFound, "managed_transition_not_found", "The managed-client transition was not found.");

        await DeactivateManagedClient(pendingClientId, appManager, vaultStore, ct);
        var cleared = ClearPendingTransition(anchor.Value.Properties);
        var updateError = await UpdateProperties(anchor.Value.Application, cleared, appManager, vaultStore, ct);
        if (updateError is not null) return updateError;
        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_TRANSITION_ABORT", "OpenShipManagedClient", pendingClientId, ct);
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new { aborted = true });
    }

    private static async Task DeactivateManagedClient(
        string clientId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        CancellationToken ct)
    {
        var application = await appManager.FindByClientIdAsync(clientId, ct);
        if (application is null) return;
        var descriptor = new OpenIddictApplicationDescriptor();
        await appManager.PopulateAsync(descriptor, application, ct);
        descriptor.Permissions.Clear();
        descriptor.RedirectUris.Clear();
        descriptor.PostLogoutRedirectUris.Clear();
        descriptor.Requirements.Clear();
        descriptor.ClientSecret = null;
        var properties = await appManager.GetPropertiesAsync(application, ct);
        SetProperties(descriptor, properties.SetItem(RetiredAtKey, JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow)));
        if (application is OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication entity)
            entity.ConcurrencyToken = Guid.NewGuid().ToString("N");
        await appManager.UpdateAsync(application, descriptor, ct);
        if (await appManager.GetClientTypeAsync(application, ct) == OpenIddictConstants.ClientTypes.Confidential)
            await vaultStore.RevokeSecretAsync(clientId, ct);
    }

    private static ImmutableDictionary<string, JsonElement> ClearPendingTransition(
        ImmutableDictionary<string, JsonElement> properties) => properties
            .Remove(PendingClientIdKey)
            .Remove(PendingTransitionIdKey)
            .Remove(PendingTransitionKindKey)
            .Remove(PendingTransitionExpiresAtKey);

    private static async Task<IResult?> ExpirePendingTransition(
        object anchorApplication,
        ImmutableDictionary<string, JsonElement> anchorProperties,
        string pendingClientId,
        string pendingTransitionId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        CancellationToken ct)
    {
        var candidate = await appManager.FindByClientIdAsync(pendingClientId, ct);
        if (candidate is not null)
        {
            var candidateProperties = await appManager.GetPropertiesAsync(candidate, ct);
            if (!string.Equals(GetString(candidateProperties, TransitionIdKey), pendingTransitionId, StringComparison.Ordinal))
                return Problem(StatusCodes.Status409Conflict, "managed_transition_ownership_conflict", "The expired candidate no longer matches the pending transition.");
            await DeactivateManagedClient(pendingClientId, appManager, vaultStore, ct);
        }
        var updateError = await UpdateProperties(anchorApplication, ClearPendingTransition(anchorProperties), appManager, vaultStore, ct);
        return updateError;
    }

    internal static async Task<bool> ExpireTransitionCandidateAsync(
        string candidateClientId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        CancellationToken ct)
    {
        var candidate = await appManager.FindByClientIdAsync(candidateClientId, ct);
        if (candidate is null) return false;
        var candidateProperties = await appManager.GetPropertiesAsync(candidate, ct);
        var expiresAt = GetDate(candidateProperties, TransitionExpiresAtKey);
        var transitionId = GetString(candidateProperties, TransitionIdKey);
        var anchorClientId = GetString(candidateProperties, AnchorClientIdKey);
        if (expiresAt is null || expiresAt.Value > DateTimeOffset.UtcNow ||
            string.IsNullOrWhiteSpace(transitionId) || string.IsNullOrWhiteSpace(anchorClientId))
            return false;

        var anchor = await appManager.FindByClientIdAsync(anchorClientId, ct);
        if (anchor is null) return false;
        var anchorProperties = await appManager.GetPropertiesAsync(anchor, ct);
        if (!string.Equals(GetString(anchorProperties, PendingClientIdKey), candidateClientId, StringComparison.Ordinal) ||
            !string.Equals(GetString(anchorProperties, PendingTransitionIdKey), transitionId, StringComparison.Ordinal))
            return false;

        var error = await ExpirePendingTransition(anchor, anchorProperties, candidateClientId,
            transitionId, appManager, vaultStore, ct);
        return error is null;
    }

    private static async Task<IResult> CreateCredentialDelivery(
        string externalKey,
        string? clientId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        HttpContext http,
        IAuditService audit,
        CancellationToken ct)
    {
        var found = await FindOwnedDeliveryClient(externalKey, clientId, appManager, http, ct);
        if (found.Error is not null) return found.Error;
        var (application, properties, resolvedClientId) = found.Value;
        if (await appManager.GetClientTypeAsync(application, ct) != OpenIddictConstants.ClientTypes.Confidential)
            return Problem(StatusCodes.Status409Conflict, "public_client_has_no_secret", "Public clients do not have a client secret.");

        var requestKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(requestKey) || requestKey.Length > 128 || !IsSafeOperationId(requestKey))
            return Problem(StatusCodes.Status400BadRequest, "idempotency_key_required", "A valid Idempotency-Key header is required.");

        var currentId = GetString(properties, DeliveryIdKey);
        var currentExpiry = GetDate(properties, DeliveryExpiresAtKey);
        var acknowledged = GetDate(properties, DeliveryAcknowledgedAtKey);
        var previousRequestKey = GetString(properties, DeliveryRequestKey);
        if (string.Equals(previousRequestKey, requestKey, StringComparison.Ordinal))
        {
            if (acknowledged is not null)
                return Problem(StatusCodes.Status409Conflict, "credential_delivery_already_acknowledged", "This idempotent credential delivery was already acknowledged.");
            if (!string.IsNullOrWhiteSpace(currentId) && currentExpiry.HasValue && currentExpiry.Value > DateTimeOffset.UtcNow)
            {
                http.Response.Headers.CacheControl = "no-store";
                http.Response.Headers.Pragma = "no-cache";
                return Results.Ok(new { operationId = currentId, expiresAt = currentExpiry });
            }
            return Problem(StatusCodes.Status410Gone, "credential_delivery_expired", "This idempotent credential delivery has expired.");
        }
        if (!string.IsNullOrWhiteSpace(currentId) && acknowledged is null && currentExpiry.HasValue && currentExpiry.Value > DateTimeOffset.UtcNow)
        {
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.Pragma = "no-cache";
            return Results.Ok(new { operationId = currentId, expiresAt = currentExpiry });
        }

        var operationId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.Add(DeliveryWindow);
        var updated = properties
            .SetItem(DeliveryIdKey, JsonSerializer.SerializeToElement(operationId))
            .SetItem(DeliveryRequestKey, JsonSerializer.SerializeToElement(requestKey))
            .SetItem(DeliveryExpiresAtKey, JsonSerializer.SerializeToElement(expiresAt))
            .Remove(DeliveryAcknowledgedAtKey);
        var updateError = await UpdateProperties(application, updated, appManager, vaultStore, ct);
        if (updateError is not null) return updateError;
        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_CREDENTIAL_DELIVERY_REQUEST", "OpenShipManagedClient", resolvedClientId, ct);
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        return Results.Ok(new { operationId, expiresAt });
    }

    private static async Task<IResult> DeliverCredential(
        string externalKey,
        string operationId,
        string? clientId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        HttpContext http,
        IAuditService audit,
        CancellationToken ct)
    {
        var found = await FindOwnedDeliveryClient(externalKey, clientId, appManager, http, ct);
        if (found.Error is not null) return found.Error;
        var (application, properties, resolvedClientId) = found.Value;
        if (!string.Equals(GetString(properties, DeliveryIdKey), operationId, StringComparison.Ordinal) ||
            GetDate(properties, DeliveryAcknowledgedAtKey) is not null ||
            !GetDate(properties, DeliveryExpiresAtKey).HasValue || GetDate(properties, DeliveryExpiresAtKey)!.Value <= DateTimeOffset.UtcNow)
        {
            return Problem(StatusCodes.Status410Gone, "credential_delivery_expired", "The credential delivery is no longer available.");
        }

        var secret = await vaultStore.GetSecretAsync(resolvedClientId, ct);
        if (string.IsNullOrWhiteSpace(secret))
            return Problem(StatusCodes.Status503ServiceUnavailable, "credential_store_unavailable", "IdentityService could not retrieve the managed credential from Vault.");

        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_CREDENTIAL_DELIVER", "OpenShipManagedClient", resolvedClientId, ct);
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        return Results.Ok(new { clientSecret = secret, expiresAt = GetDate(properties, DeliveryExpiresAtKey) });
    }

    private static async Task<IResult> AcknowledgeCredentialDelivery(
        string externalKey,
        string operationId,
        string? clientId,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        HttpContext http,
        IAuditService audit,
        CancellationToken ct)
    {
        var found = await FindOwnedDeliveryClient(externalKey, clientId, appManager, http, ct);
        if (found.Error is not null) return found.Error;
        var (application, properties, resolvedClientId) = found.Value;
        if (string.Equals(GetString(properties, DeliveryIdKey), operationId, StringComparison.Ordinal) &&
            GetDate(properties, DeliveryAcknowledgedAtKey) is not null)
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { acknowledged = true, alreadyAcknowledged = true });
        }
        if (!string.Equals(GetString(properties, DeliveryIdKey), operationId, StringComparison.Ordinal) ||
            !GetDate(properties, DeliveryExpiresAtKey).HasValue || GetDate(properties, DeliveryExpiresAtKey)!.Value <= DateTimeOffset.UtcNow)
        {
            return Problem(StatusCodes.Status410Gone, "credential_delivery_expired", "The credential delivery is no longer available.");
        }

        var updated = properties.SetItem(DeliveryAcknowledgedAtKey, JsonSerializer.SerializeToElement(DateTimeOffset.UtcNow));
        var updateError = await UpdateProperties(application, updated, appManager, vaultStore, ct);
        if (updateError is not null) return updateError;
        await AdminAudit.LogAsync(audit, http, "OPENSHIP_CLIENT_CREDENTIAL_DELIVERY_ACK", "OpenShipManagedClient", resolvedClientId, ct);
        return Results.Ok(new { acknowledged = true });
    }

    private static async Task<((object Application, ImmutableDictionary<string, JsonElement> Properties, string ClientId) Value, IResult? Error)> FindOwnedClient(
        string externalKey,
        IOpenIddictApplicationManager appManager,
        HttpContext http,
        CancellationToken ct)
    {
        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return (default, anchor.Error);
        var activeClientId = GetString(anchor.Value.Properties, ActiveClientIdKey) ?? anchor.Value.ClientId;
        var application = await appManager.FindByClientIdAsync(activeClientId, ct);
        if (application is null)
            return (default, Problem(StatusCodes.Status409Conflict, "managed_client_unavailable", "The active managed client was not found."));
        var properties = await appManager.GetPropertiesAsync(application, ct);
        if (!Owns(properties, CallerClientId(http)!, externalKey))
            return (default, Problem(StatusCodes.Status409Conflict, "managed_client_ownership_conflict", "The active client is not owned by this OpenShip workload."));
        return (((application, properties, activeClientId)), null);
    }

    private static async Task<((object Application, ImmutableDictionary<string, JsonElement> Properties, string ClientId) Value, IResult? Error)> FindOwnedAnchor(
        string externalKey,
        IOpenIddictApplicationManager appManager,
        HttpContext http,
        CancellationToken ct)
    {
        var ownerClientId = CallerClientId(http);
        if (!IsValidExternalKey(externalKey) || string.IsNullOrWhiteSpace(ownerClientId) || !ExternalKeyBelongsTo(externalKey, ownerClientId))
            return (default, Problem(StatusCodes.Status403Forbidden, "external_key_forbidden", "The external key is not owned by this workload client."));
        var clientId = ManagedClientId(externalKey);
        var application = await appManager.FindByClientIdAsync(clientId, ct);
        if (application is null)
            return (default, Problem(StatusCodes.Status404NotFound, "managed_client_not_found", "The managed client was not found."));
        var properties = await appManager.GetPropertiesAsync(application, ct);
        if (!Owns(properties, ownerClientId, externalKey))
            return (default, Problem(StatusCodes.Status409Conflict, "managed_client_ownership_conflict", "The client is not owned by this OpenShip workload."));
        return (((application, properties, clientId)), null);
    }

    private static async Task<((object Application, ImmutableDictionary<string, JsonElement> Properties, string ClientId) Value, IResult? Error)> FindOwnedDeliveryClient(
        string externalKey,
        string? requestedClientId,
        IOpenIddictApplicationManager appManager,
        HttpContext http,
        CancellationToken ct)
    {
        var anchor = await FindOwnedAnchor(externalKey, appManager, http, ct);
        if (anchor.Error is not null) return (default, anchor.Error);
        var activeClientId = GetString(anchor.Value.Properties, ActiveClientIdKey) ?? anchor.Value.ClientId;
        var pendingClientId = GetString(anchor.Value.Properties, PendingClientIdKey);
        var clientId = string.IsNullOrWhiteSpace(requestedClientId) ? activeClientId : requestedClientId.Trim();
        if (!string.Equals(clientId, activeClientId, StringComparison.Ordinal) &&
            !string.Equals(clientId, pendingClientId, StringComparison.Ordinal))
        {
            return (default, Problem(StatusCodes.Status403Forbidden, "managed_client_target_forbidden", "Credential delivery is limited to the active or pending managed client."));
        }
        var application = await appManager.FindByClientIdAsync(clientId, ct);
        if (application is null)
            return (default, Problem(StatusCodes.Status404NotFound, "managed_client_not_found", "The managed client was not found."));
        var properties = await appManager.GetPropertiesAsync(application, ct);
        var ownerClientId = CallerClientId(http);
        if (string.IsNullOrWhiteSpace(ownerClientId) || !Owns(properties, ownerClientId, externalKey))
            return (default, Problem(StatusCodes.Status409Conflict, "managed_client_ownership_conflict", "The credential target is not owned by this OpenShip workload."));
        return (((application, properties, clientId)), null);
    }

    private static void SetProperties(
        OpenIddictApplicationDescriptor descriptor,
        ImmutableDictionary<string, JsonElement> properties)
    {
        descriptor.Properties.Clear();
        foreach (var property in properties)
            descriptor.Properties[property.Key] = property.Value;
    }

    private static async Task<IResult?> UpdateProperties(
        object application,
        ImmutableDictionary<string, JsonElement> properties,
        IOpenIddictApplicationManager appManager,
        VaultClientSecretStore vaultStore,
        CancellationToken ct)
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await appManager.PopulateAsync(descriptor, application, ct);
        SetProperties(descriptor, properties);
        var isRetired = GetDate(properties, RetiredAtKey) is not null;
        if (await appManager.GetClientTypeAsync(application, ct) == OpenIddictConstants.ClientTypes.Confidential && !isRetired)
        {
            descriptor.ClientSecret = await vaultStore.GetSecretAsync(descriptor.ClientId!, ct);
            if (string.IsNullOrWhiteSpace(descriptor.ClientSecret))
                return Problem(StatusCodes.Status503ServiceUnavailable, "credential_store_unavailable", "IdentityService could not retrieve the managed credential from Vault.");
        }
        else if (await appManager.GetClientTypeAsync(application, ct) == OpenIddictConstants.ClientTypes.Confidential)
        {
            descriptor.ClientSecret = null;
        }
        if (application is OpenIddictEntityFrameworkCore.OpenIddictEntityFrameworkCoreApplication entity)
            entity.ConcurrencyToken = Guid.NewGuid().ToString("N");
        try
        {
            await appManager.UpdateAsync(application, descriptor, ct);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(StatusCodes.Status409Conflict, "managed_client_changed", "The managed client changed during the credential delivery operation.");
        }
    }

    private static bool TryNormalize(ManagedClientRequest request, out NormalizedManagedClient normalized, out string? error)
    {
        normalized = default!;
        error = null;
        if (request.ProfileRevision < 0 || request.ServiceRevision < 0)
            error = "Profile and service revisions cannot be negative.";
        else if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 128)
            error = "Display name must contain 1 to 128 characters.";
        else if (request.ClientType is not ("public" or "confidential"))
            error = "Client type must be public or confidential.";
        else if (request.GrantTypes is null || request.ResponseTypes is null || request.RedirectUris is null || request.PostLogoutRedirectUris is null || request.Scopes is null)
            error = "Grant, response, redirect, and scope lists are required.";
        if (error is not null) return false;

        var clientType = request.ClientType == "public"
            ? OpenIddictConstants.ClientTypes.Public
            : OpenIddictConstants.ClientTypes.Confidential;
        var grants = request.GrantTypes!.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var responses = request.ResponseTypes!.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var redirects = NormalizeUris(request.RedirectUris!, out error);
        if (error is not null) return false;
        var logoutRedirects = NormalizeUris(request.PostLogoutRedirectUris!, out error);
        if (error is not null) return false;
        var requestedScopes = request.Scopes!;
        if (requestedScopes.Any(string.IsNullOrWhiteSpace))
        {
            error = "Scope names cannot be empty.";
            return false;
        }
        var scopes = requestedScopes.Select(scope => scope.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (scopes.Contains(IdentityApiRoutes.OpenShipClientReconcileScope, StringComparer.Ordinal))
            error = "The OpenShip provisioning scope cannot be delegated to a managed service client.";
        if (error is not null) return false;

        var allowedGrants = clientType == OpenIddictConstants.ClientTypes.Public
            ? new[] { "authorization_code", "refresh_token" }
            : new[] { "authorization_code", "refresh_token", "client_credentials" };
        if (grants.Length == 0 || grants.Any(grant => !allowedGrants.Contains(grant, StringComparer.Ordinal)))
            error = "The requested grant types are not supported for this client type.";
        else if (grants.Contains("authorization_code", StringComparer.Ordinal) != responses.Contains("code", StringComparer.Ordinal) || responses.Any(response => response != "code"))
            error = "Authorization-code clients must use only the code response type.";
        else if (grants.Contains("refresh_token", StringComparer.Ordinal) && !grants.Contains("authorization_code", StringComparer.Ordinal))
            error = "Refresh tokens require the authorization_code grant.";
        else if (scopes.Contains("openid", StringComparer.Ordinal) && !grants.Contains("authorization_code", StringComparer.Ordinal))
            error = "The openid scope requires an authorization-code flow.";
        else if (scopes.Contains("offline_access", StringComparer.Ordinal) && !grants.Contains("refresh_token", StringComparer.Ordinal))
            error = "The offline_access scope requires the refresh_token grant.";
        else if (clientType == OpenIddictConstants.ClientTypes.Public && (!grants.Contains("authorization_code", StringComparer.Ordinal) || !scopes.Contains("openid", StringComparer.Ordinal)))
            error = "Public clients require authorization_code and the openid scope.";
        else if (clientType == OpenIddictConstants.ClientTypes.Confidential && grants.Contains("client_credentials", StringComparer.Ordinal) && grants.Contains("authorization_code", StringComparer.Ordinal))
            error = "A confidential client must use either client_credentials or authorization_code, not both flows.";
        else if ((grants.Contains("authorization_code", StringComparer.Ordinal)) != (redirects.Length > 0))
            error = "Redirect URIs are required exactly for authorization-code clients.";
        else if (!grants.Contains("authorization_code", StringComparer.Ordinal) && logoutRedirects.Length > 0)
            error = "Post-logout redirect URIs require authorization_code.";
        else if (scopes.Length > 32 || scopes.Any(scope => !System.Text.RegularExpressions.Regex.IsMatch(scope, "^[A-Za-z0-9:._-]{1,64}$")))
            error = "Scopes contain an invalid or unsupported value.";
        if (error is not null) return false;

        var permissions = new HashSet<string>(StringComparer.Ordinal)
        {
            OpenIddictConstants.Permissions.Endpoints.Token,
        };
        if (grants.Contains("authorization_code", StringComparer.Ordinal))
        {
            permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        }
        if (grants.Contains("refresh_token", StringComparer.Ordinal))
            permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        if (grants.Contains("client_credentials", StringComparer.Ordinal))
            permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        foreach (var scope in scopes)
        {
            permissions.Add(scope switch
            {
                "email" => OpenIddictConstants.Permissions.Scopes.Email,
                "profile" => OpenIddictConstants.Permissions.Scopes.Profile,
                "roles" => OpenIddictConstants.Permissions.Scopes.Roles,
                _ => OpenIddictConstants.Permissions.Prefixes.Scope + scope,
            });
        }
        normalized = new NormalizedManagedClient(
            request.DisplayName.Trim(), clientType, grants, responses, redirects, logoutRedirects,
            scopes, permissions, request.ProfileRevision, request.ServiceRevision);
        return true;
    }

    private static string[] NormalizeUris(IReadOnlyCollection<string>? values, out string? error)
    {
        error = null;
        if (values is null || values.Count > 16)
        {
            error = "Redirect URI lists must contain at most 16 entries.";
            return [];
        }
        var normalized = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                uri.IsLoopback || IsPrivateAddress(uri.Host))
            {
                error = "Redirect URIs must be absolute public HTTPS URLs without credentials, query, or fragment.";
                return [];
            }
            normalized.Add(uri.AbsoluteUri);
        }
        return normalized.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool IsPrivateAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var address)) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) ||
                (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) || bytes[0] >= 224;
        }
        var ipv6 = address.GetAddressBytes();
        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.Equals(IPAddress.IPv6Any) ||
            (ipv6[0] & 0xFE) == 0xFC || (ipv6[0] == 0x20 && ipv6[1] == 0x01 && ipv6[2] == 0x0D && ipv6[3] == 0xB8);
    }

    private static bool IsValidExternalKey(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, "^os-[a-f0-9]{12}-[a-f0-9]{32}$");

    private static bool IsSafeOperationId(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z0-9._:-]{1,128}$");

    private static string ManagedClientId(string externalKey) => $"os-{Hash(externalKey)[..32]}";

    private static bool ExternalKeyBelongsTo(string externalKey, string ownerClientId) =>
        externalKey.StartsWith($"os-{Hash(ownerClientId)[..12]}-", StringComparison.Ordinal);

    private static string? CallerClientId(HttpContext http) =>
        http.User.FindFirst("client_id")?.Value ?? http.User.FindFirst("azp")?.Value;

    private static bool Owns(ImmutableDictionary<string, JsonElement> properties, string ownerClientId, string externalKey) =>
        string.Equals(GetString(properties, OwnerClientIdKey), ownerClientId, StringComparison.Ordinal) &&
        string.Equals(GetString(properties, ExternalKeyKey), externalKey, StringComparison.Ordinal);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string? GetString(ImmutableDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long GetLong(ImmutableDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static DateTimeOffset? GetDate(ImmutableDictionary<string, JsonElement> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var date) ? date : null;

    private static IResult ManagedClientResponse(
        HttpContext http,
        string externalKey,
        string clientId,
        string? version,
        string? credentialVersion,
        bool created,
        string? transitionId = null,
        string? transitionKind = null,
        DateTimeOffset? transitionExpiresAt = null,
        string? activeClientId = null)
    {
        if (!string.IsNullOrWhiteSpace(version)) http.Response.Headers.ETag = $"\"{version}\"";
        http.Response.Headers.CacheControl = "no-store";
        var response = new
        {
            clientId,
            registrationVersion = version,
            credentialVersion,
            status = transitionId is null ? "managed" : "transition_pending",
            transitionId,
            transitionKind,
            transitionExpiresAt,
            activeClientId,
        };
        return created ? Results.Created($"{IdentityApiRoutes.OpenShipProvisioningClients}/{Uri.EscapeDataString(externalKey)}", response) : Results.Ok(response);
    }

    private static IResult Problem(int status, string code, string detail) =>
        Results.Problem(statusCode: status, detail: detail, extensions: new Dictionary<string, object?> { ["errorCode"] = code });

    public sealed record ManagedClientRequest(
        string DisplayName,
        string ClientType,
        string[] GrantTypes,
        string[] ResponseTypes,
        string[] RedirectUris,
        string[] PostLogoutRedirectUris,
        string[] Scopes,
        long ProfileRevision,
        long ServiceRevision);

    private sealed record NormalizedManagedClient(
        string DisplayName,
        string ClientType,
        string[] GrantTypes,
        string[] ResponseTypes,
        string[] RedirectUris,
        string[] PostLogoutRedirectUris,
        string[] Scopes,
        HashSet<string> Permissions,
        long ProfileRevision,
        long ServiceRevision);
}
