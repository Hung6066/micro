using System.Text;
using System.Text.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using StackExchange.Redis;
using His.Hope.IdentityService.Api.Services;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class PasskeyEndpoints
{
    public static void MapPasskeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth/passkeys").RequireAuthorization();

        group.MapGet("/status", async (HttpContext context, IdentityDbContext db, CancellationToken ct) =>
        {
            var userId = GetUserId(context);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

            var credentials = await db.PasskeyCredentials
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new { item.CredentialId, item.CreatedAt })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                registered = credentials.Count > 0,
                count = credentials.Count,
                createdAt = credentials.OrderByDescending(item => item.CreatedAt).Select(item => item.CreatedAt).FirstOrDefault()
            });
        });

        group.MapPost("/register/options", async (HttpContext context, Fido2 fido2, IConnectionMultiplexer redis) =>
        {
            var userId = GetUserId(context);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var user = new Fido2User { DisplayName = userId, Name = userId, Id = Encoding.UTF8.GetBytes(userId) };
            var options = fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = user,
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Required,
                    UserVerification = UserVerificationRequirement.Required
                }
            });
            await redis.GetDatabase().StringSetAsync(OptionsKey(userId), options.ToJson(), TimeSpan.FromMinutes(5));
            return Results.Ok(options);
        });

        group.MapPost("/register/complete", async (HttpContext context, AuthenticatorAttestationRawResponse response, Fido2 fido2, IConnectionMultiplexer redis, IdentityDbContext db) =>
        {
            var userId = GetUserId(context);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var redisDb = redis.GetDatabase();
            var rawOptions = await redisDb.StringGetDeleteAsync(OptionsKey(userId));
            if (!rawOptions.HasValue) return Results.BadRequest(new ProblemDetails { Title = "Passkey challenge expired", Status = 400 });
            var options = CredentialCreateOptions.FromJson(rawOptions!);
            var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) =>
                    !await db.PasskeyCredentials.AnyAsync(
                        credential => credential.CredentialId == Convert.ToBase64String(args.CredentialId),
                        cancellationToken)
            });
            var credentialId = Convert.ToBase64String(result.Id);
            db.PasskeyCredentials.Add(new PasskeyCredential
            {
                UserId = userId,
                CredentialId = credentialId,
                PublicKey = Convert.ToBase64String(result.PublicKey),
                SignatureCounter = result.SignCount
            });
            await db.SaveChangesAsync();
            await redis.GetDatabase().StringSetAsync(CredentialPointerKey(userId), credentialId, flags: CommandFlags.DemandMaster);
            return Results.Ok(new { registered = true });
        });

        var login = app.MapGroup("/api/v1/auth/passkeys")
            .AllowAnonymous()
            .RequireRateLimiting("auth");
        login.MapPost("/authenticate/options", async (PasskeyUserRequest request, Fido2 fido2, IConnectionMultiplexer redis, IdentityDbContext db, UserManager<User> users) =>
        {
            var requestedUserId = request.UserId;
            if (string.IsNullOrWhiteSpace(requestedUserId) && !string.IsNullOrWhiteSpace(request.UserName))
                requestedUserId = (await users.FindByEmailAsync(request.UserName))?.Id.ToString();

            if (string.IsNullOrWhiteSpace(requestedUserId))
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Passkey account is required", Detail = "Enter the email address associated with your passkey." });
            var credential = await db.PasskeyCredentials.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == requestedUserId);
            if (credential is null)
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Passkey is not enrolled", Detail = "Register a passkey for this account before signing in with it." });
            var credentialId = credential.CredentialId;
            var redisDb = redis.GetDatabase();
            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = new[] { new PublicKeyCredentialDescriptor(Convert.FromBase64String(credentialId!)) },
                UserVerification = UserVerificationRequirement.Required
            });
            await redisDb.StringSetAsync(CredentialPointerKey(requestedUserId), credentialId, TimeSpan.FromMinutes(5));
            await redisDb.StringSetAsync(AssertionKey(requestedUserId), options.ToJson(), TimeSpan.FromMinutes(5));
            return Results.Ok(new { userId = requestedUserId, options });
        });

        login.MapPost("/authenticate/complete", async (PasskeyAssertionRequest request, HttpContext context, Fido2 fido2, IConnectionMultiplexer redis,
            UserManager<User> users, OidcLoginCompletionService completion, IdentityDbContext db) =>
        {
            var redisDb = redis.GetDatabase();
            var rawOptions = await redisDb.StringGetDeleteAsync(AssertionKey(request.UserId));
            var credentialId = await redisDb.StringGetAsync(CredentialPointerKey(request.UserId));
            if (!rawOptions.HasValue || !credentialId.HasValue) return Results.Unauthorized();
            var stored = await db.PasskeyCredentials.SingleOrDefaultAsync(
                credential => credential.UserId == request.UserId && credential.CredentialId == credentialId.ToString());
            if (stored is null) return Results.Unauthorized();
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = request.Response,
                OriginalOptions = AssertionOptions.FromJson(rawOptions!),
                StoredPublicKey = Convert.FromBase64String(stored.PublicKey),
                StoredSignatureCounter = stored.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(args.UserHandle is { Length: > 0 } &&
                        args.UserHandle.SequenceEqual(Encoding.UTF8.GetBytes(request.UserId)))
            });
            stored.SignatureCounter = result.SignCount;
            stored.LastUsedAt = DateTime.UtcNow;
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null || !user.IsActive) return Results.Unauthorized();
            await db.SaveChangesAsync();
            var completed = await completion.CompletePrimaryAsync(context, user, request.ReturnUrl, ["passkey"]);
            return Results.Ok(new
            {
                authenticated = !completed.RequiresMfa,
                requiresMfa = completed.RequiresMfa,
                redirectUrl = completed.RedirectUrl
            });
        });

        login.MapPost("/mfa/options", async (
            HttpContext context,
            OidcLoginCompletionService completion,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            IdentityDbContext db) =>
        {
            var pending = completion.TryGetPendingMfaContext(context);
            if (pending is null) return Results.Unauthorized();
            var userId = pending.UserId;

            var credential = await db.PasskeyCredentials.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == userId.ToString());
            if (credential is null)
                return Results.UnprocessableEntity(new ProblemDetails
                {
                    Title = "MFA passkey is not enrolled",
                    Detail = "Register a passkey before using it as the MFA factor."
                });

            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = new[] { new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.CredentialId)) },
                UserVerification = UserVerificationRequirement.Required
            });
            var redisDb = redis.GetDatabase();
            await redisDb.StringSetAsync(MfaAssertionKey(pending), options.ToJson(), TimeSpan.FromMinutes(5));
            await redisDb.StringSetAsync(MfaCredentialPointerKey(pending), credential.CredentialId, TimeSpan.FromMinutes(5));
            return Results.Ok(new { userId, options });
        });

        login.MapPost("/mfa/complete", async (
            PasskeyAssertionRequest request,
            HttpContext context,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion,
            IdentityDbContext db) =>
        {
            var pending = completion.TryGetPendingMfaContext(context);
            if (pending is null)
                return Results.Unauthorized();

            if (!string.IsNullOrWhiteSpace(request.UserId) &&
                (!Guid.TryParse(request.UserId, out var requestedUserId) || requestedUserId != pending.UserId))
            {
                return Results.Conflict();
            }

            var userId = pending.UserId;
            var redisDb = redis.GetDatabase();
            var rawOptions = await redisDb.StringGetDeleteAsync(MfaAssertionKey(pending));
            var credentialId = await redisDb.StringGetDeleteAsync(MfaCredentialPointerKey(pending));
            if (!rawOptions.HasValue || !credentialId.HasValue)
                return Results.Unauthorized();

            var stored = await db.PasskeyCredentials.SingleOrDefaultAsync(
                credential => credential.UserId == userId.ToString() && credential.CredentialId == credentialId.ToString());
            if (stored is null) return Results.Unauthorized();

            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = request.Response,
                OriginalOptions = AssertionOptions.FromJson(rawOptions!),
                StoredPublicKey = Convert.FromBase64String(stored.PublicKey),
                StoredSignatureCounter = stored.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(args.UserHandle is { Length: > 0 } &&
                        args.UserHandle.SequenceEqual(Encoding.UTF8.GetBytes(userId.ToString())))
            });

            stored.SignatureCounter = result.SignCount;
            stored.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var redirectUrl = await completion.CompleteMfaWithPasskeyAsync(context, userId, CancellationToken.None);
            return redirectUrl is null
                ? Results.Unauthorized()
                : Results.Ok(new { redirectUrl });
        });

        login.MapPost("/mfa/native/start", async (
            HttpContext context,
            OidcLoginCompletionService completion,
            IConnectionMultiplexer redis) =>
        {
            var pending = completion.TryGetPendingMfaContext(context);
            if (pending is null) return Results.Unauthorized();

            var ticket = CreateNativeMfaTicket();
            var state = new NativeMfaState(
                pending.UserId,
                pending.PendingId,
                pending.SessionId,
                false,
                DateTimeOffset.UtcNow);
            await redis.GetDatabase().StringSetAsync(NativeMfaKey(ticket), JsonSerializer.Serialize(state), TimeSpan.FromMinutes(5));
            return Results.Ok(new { ticket, deepLink = $"hishope://auth/mfa?ticket={Uri.EscapeDataString(ticket)}" });
        });

        login.MapGet("/mfa/native/poll", async (
            string ticket,
            HttpContext context,
            OidcLoginCompletionService completion,
            IConnectionMultiplexer redis) =>
        {
            var state = await ReadNativeMfaState(redis, ticket);
            var pending = completion.TryGetPendingMfaContext(context);
            if (state is null ||
                pending is null ||
                state.UserId != pending.UserId ||
                !string.Equals(state.PendingId, pending.PendingId, StringComparison.Ordinal) ||
                !string.Equals(state.SessionId, pending.SessionId, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }
            if (!state.Approved) return Results.Accepted();

            await redis.GetDatabase().KeyDeleteAsync(NativeMfaKey(ticket));
            var redirectUrl = await completion.CompleteMfaWithPasskeyAsync(context, state.UserId, CancellationToken.None);
            return redirectUrl is null
                ? Results.Unauthorized()
                : Results.Ok(new { redirectUrl });
        });

        login.MapPost("/mfa/native/options", async (
            NativeMfaTicketRequest request,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion,
            IdentityDbContext db) =>
        {
            var state = await ReadNativeMfaState(redis, request.Ticket);
            if (state is null ||
                state.Approved ||
                !completion.HasLivePendingMfaContext(state.PendingId, state.SessionId, state.UserId))
            {
                return Results.Unauthorized();
            }

            var credential = await db.PasskeyCredentials.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == state.UserId.ToString());
            if (credential is null)
                return Results.UnprocessableEntity(new ProblemDetails
                {
                    Title = "MFA passkey is not enrolled",
                    Detail = "Register a passkey for this device before using native MFA."
                });

            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = new[] { new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.CredentialId)) },
                UserVerification = UserVerificationRequirement.Required
            });
            var redisDb = redis.GetDatabase();
            await redisDb.StringSetAsync(NativeMfaOptionsKey(request.Ticket), options.ToJson(), TimeSpan.FromMinutes(5));
            await redisDb.StringSetAsync(NativeMfaCredentialKey(request.Ticket), credential.CredentialId, TimeSpan.FromMinutes(5));
            return Results.Ok(new { options });
        });

        login.MapPost("/mfa/native/complete", async (
            NativeMfaAssertionRequest request,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion,
            IdentityDbContext db) =>
        {
            var state = await ReadNativeMfaState(redis, request.Ticket);
            if (state is null ||
                state.Approved ||
                !completion.HasLivePendingMfaContext(state.PendingId, state.SessionId, state.UserId))
            {
                return Results.Unauthorized();
            }

            var redisDb = redis.GetDatabase();
            var rawOptions = await redisDb.StringGetDeleteAsync(NativeMfaOptionsKey(request.Ticket));
            var credentialId = await redisDb.StringGetDeleteAsync(NativeMfaCredentialKey(request.Ticket));
            if (!rawOptions.HasValue || !credentialId.HasValue) return Results.Unauthorized();

            var stored = await db.PasskeyCredentials.SingleOrDefaultAsync(
                credential => credential.UserId == state.UserId.ToString() && credential.CredentialId == credentialId.ToString());
            if (stored is null) return Results.Unauthorized();

            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = request.Response,
                OriginalOptions = AssertionOptions.FromJson(rawOptions!),
                StoredPublicKey = Convert.FromBase64String(stored.PublicKey),
                StoredSignatureCounter = stored.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(args.UserHandle is { Length: > 0 } &&
                        args.UserHandle.SequenceEqual(Encoding.UTF8.GetBytes(state.UserId.ToString())))
            });

            stored.SignatureCounter = result.SignCount;
            stored.LastUsedAt = DateTime.UtcNow;
            state = state with { Approved = true };
            await db.SaveChangesAsync();
            await redisDb.StringSetAsync(NativeMfaKey(request.Ticket), JsonSerializer.Serialize(state), TimeSpan.FromMinutes(2));
            return Results.Ok(new { approved = true });
        });
    }

    private static string OptionsKey(string userId) => $"hishop:passkey:registration:{userId}";
    private static string CredentialPointerKey(string userId) => $"hishop:passkey:credential-pointer:{userId}";
    private static string AssertionKey(string userId) => $"hishop:passkey:assertion:{userId}";
    private static string MfaAssertionKey(PendingMfaContext pending) =>
        $"hishop:passkey:mfa:assertion:{pending.UserId:D}:{pending.PendingId}:{pending.SessionId}";
    private static string MfaCredentialPointerKey(PendingMfaContext pending) =>
        $"hishop:passkey:mfa:credential:{pending.UserId:D}:{pending.PendingId}:{pending.SessionId}";
    private static string NativeMfaKey(string ticket) => $"hishop:passkey:mfa:native:{ticket}";
    private static string NativeMfaOptionsKey(string ticket) => $"hishop:passkey:mfa:native:options:{ticket}";
    private static string NativeMfaCredentialKey(string ticket) => $"hishop:passkey:mfa:native:credential:{ticket}";

    private static string CreateNativeMfaTicket() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static async Task<NativeMfaState?> ReadNativeMfaState(IConnectionMultiplexer redis, string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 128) return null;
        var raw = await redis.GetDatabase().StringGetAsync(NativeMfaKey(ticket));
        return raw.HasValue ? JsonSerializer.Deserialize<NativeMfaState>(raw!) : null;
    }

    private static string? GetUserId(HttpContext context) =>
        context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public sealed record PasskeyUserRequest(string? UserId, string? UserName);
    public sealed record PasskeyAssertionRequest(string UserId, AuthenticatorAssertionRawResponse Response, string? ReturnUrl);
    public sealed record NativeMfaTicketRequest(string Ticket);
    public sealed record NativeMfaAssertionRequest(string Ticket, AuthenticatorAssertionRawResponse Response);
    private sealed record NativeMfaState(
        Guid UserId,
        string PendingId,
        string SessionId,
        bool Approved,
        DateTimeOffset CreatedAt);
}
