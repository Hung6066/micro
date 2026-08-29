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
using His.Hope.Contracts;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using StackExchange.Redis;
using His.Hope.IdentityService.Api.Services;
using His.Hope.Contracts.Identity;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class PasskeyEndpoints
{
    private static readonly TimeSpan NativeMfaTicketLifetime = TimeSpan.FromMinutes(5);

    public static void MapPasskeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(IdentityApiRoutes.Passkeys).RequireAuthorization();

        group.MapGet(IdentityApiRoutes.PasskeyStatusSegment, async (HttpContext context, IdentityDbContext db, CancellationToken ct) =>
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
                minimumRequired = 2,
                meetsMinimum = credentials.Count >= 2,
                createdAt = credentials.OrderByDescending(item => item.CreatedAt).Select(item => item.CreatedAt).FirstOrDefault()
            });
        });

        group.MapPost(IdentityApiRoutes.PasskeyRegisterOptionsSegment, async (HttpContext context, Fido2 fido2, IConnectionMultiplexer redis) =>
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

        group.MapPost(IdentityApiRoutes.PasskeyRegisterCompleteSegment, async (HttpContext context, AuthenticatorAttestationRawResponse response, Fido2 fido2, IConnectionMultiplexer redis, IdentityDbContext db, CancellationToken ct) =>
        {
            var userId = GetUserId(context);
            if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
            var redisDb = redis.GetDatabase();
            var rawOptions = await redisDb.StringGetDeleteAsync(OptionsKey(userId));
            if (!rawOptions.HasValue) return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasskeyChallengeExpired });
            try
            {
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
                await db.SaveChangesAsync(ct);
                await redis.GetDatabase().StringSetAsync(CredentialPointerKey(userId), credentialId, flags: CommandFlags.DemandMaster);
                return Results.Ok(new { registered = true });
            }
            catch (Exception)
            {
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidPasskeyAttestation });
            }
        });

        var login = app.MapGroup(IdentityApiRoutes.Passkeys)
            .AllowAnonymous()
            .RequireRateLimiting("auth");
        login.MapPost(IdentityApiRoutes.PasskeyAuthenticateOptionsSegment, async (PasskeyUserRequest request, Fido2 fido2, IConnectionMultiplexer redis, IdentityDbContext db, UserManager<User> users, CancellationToken ct) =>
        {
            var requestedUserId = request.UserId;
            if (string.IsNullOrWhiteSpace(requestedUserId) && !string.IsNullOrWhiteSpace(request.UserName))
                requestedUserId = (await users.FindByEmailAsync(request.UserName))?.Id.ToString();

            if (string.IsNullOrWhiteSpace(requestedUserId))
                return Results.UnprocessableEntity(new { errorCode = "passkey_account_required" });
            var credentials = await db.PasskeyCredentials.AsNoTracking()
                .Where(item => item.UserId == requestedUserId)
                .Select(item => item.CredentialId)
                .ToListAsync(ct);
            if (credentials.Count == 0)
                return Results.UnprocessableEntity(new { errorCode = "passkey_not_enrolled", message = "Passkey not enrolled." });
            var redisDb = redis.GetDatabase();
            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = credentials
                    .Select(credentialId => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credentialId)))
                    .ToArray(),
                UserVerification = UserVerificationRequirement.Required
            });
            await redisDb.StringSetAsync(AssertionKey(requestedUserId), options.ToJson(), TimeSpan.FromMinutes(5));
            return Results.Ok(new { userId = requestedUserId, options });
        });

        login.MapPost(IdentityApiRoutes.PasskeyAuthenticateCompleteSegment, async (PasskeyAssertionRequest request, HttpContext context, Fido2 fido2, IConnectionMultiplexer redis,
            UserManager<User> users, OidcLoginCompletionService completion, IdentityDbContext db, CancellationToken ct) =>
        {
            var redisDb = redis.GetDatabase();
            var rawOptions = await redisDb.StringGetDeleteAsync(AssertionKey(request.UserId));
            if (!rawOptions.HasValue || string.IsNullOrWhiteSpace(request.Response.Id)) return Results.Unauthorized();
            var stored = await db.PasskeyCredentials.SingleOrDefaultAsync(
                credential => credential.UserId == request.UserId && credential.CredentialId == request.Response.Id, ct);
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
            await db.SaveChangesAsync(ct);
            var completed = await completion.CompletePrimaryAsync(context, user, request.ReturnUrl, ["passkey"]);
            return Results.Ok(new
            {
                authenticated = !completed.RequiresMfa,
                requiresMfa = completed.RequiresMfa,
                redirectUrl = completed.RedirectUrl
            });
        });

        login.MapPost(IdentityApiRoutes.PasskeyMfaOptionsSegment, async (
            HttpContext context,
            OidcLoginCompletionService completion,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            IdentityDbContext db, CancellationToken ct) =>
        {
            var pending = completion.TryGetPendingMfaContext(context);
            if (pending is null) return Results.Unauthorized();
            var userId = pending.UserId;

            var credentials = await db.PasskeyCredentials.AsNoTracking()
                .Where(item => item.UserId == userId.ToString())
                .Select(item => item.CredentialId)
                .ToListAsync(ct);
            if (credentials.Count == 0)
                return Results.UnprocessableEntity(new { errorCode = "mfa_passkey_not_enrolled", message = "MFA passkey not enrolled." });

            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = credentials
                    .Select(credentialId => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credentialId)))
                    .ToArray(),
                UserVerification = UserVerificationRequirement.Required
            });
            var redisDb = redis.GetDatabase();
            await redisDb.StringSetAsync(MfaAssertionKey(pending), options.ToJson(), TimeSpan.FromMinutes(5));
            return Results.Ok(new { userId, options });
        });

        login.MapPost(IdentityApiRoutes.PasskeyMfaCompleteSegment, async (
            PasskeyAssertionRequest request,
            HttpContext context,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion,
            IdentityDbContext db, CancellationToken ct) =>
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
            // Use a small atomic Lua GET+DEL instead of Redis GETDEL so the
            // flow works with the minimum Redis version supported by local
            // test/dev installations as well as Redis 6.2+.
            var rawOptions = await GetAndDeleteAsync(redisDb, MfaAssertionKey(pending));
            if (!rawOptions.HasValue || string.IsNullOrWhiteSpace(request.Response.Id))
                return Results.Unauthorized();

            var stored = await db.PasskeyCredentials.SingleOrDefaultAsync(
                credential => credential.UserId == userId.ToString() && credential.CredentialId == request.Response.Id, ct);
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
            await db.SaveChangesAsync(ct);

            var redirectUrl = await completion.CompleteMfaWithPasskeyAsync(context, userId, ct);
            return redirectUrl is null
                ? Results.Unauthorized()
                : Results.Ok(new { redirectUrl });
        });

        login.MapPost(IdentityApiRoutes.NativeMfaStartSegment, async (
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
                false,
                DateTimeOffset.UtcNow);
            await redis.GetDatabase().StringSetAsync(NativeMfaKey(ticket), JsonSerializer.Serialize(state), NativeMfaTicketLifetime);
            return Results.Ok(new
            {
                ticket,
                deepLink = $"hishope://auth/mfa?ticket={Uri.EscapeDataString(ticket)}",
                expiresInMs = (int)NativeMfaTicketLifetime.TotalMilliseconds
            });
        });

        login.MapGet(IdentityApiRoutes.NativeMfaPollSegment, async (
            string ticket,
            HttpContext context,
            OidcLoginCompletionService completion,
            IConnectionMultiplexer redis,
            CancellationToken ct) =>
        {
            var pending = completion.TryGetPendingMfaContext(context);
            if (pending is null)
            {
                return Results.Unauthorized();
            }

            var state = await ReadNativeMfaState(redis, ticket);
            if (state is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status410Gone, extensions: new Dictionary<string, object?>
                {
                    [ApiProblemExtensions.ErrorCode] = "mobile_approval_expired"
                });
            }

            if (state.UserId != pending.UserId ||
                !string.Equals(state.PendingId, pending.PendingId, StringComparison.Ordinal) ||
                !string.Equals(state.SessionId, pending.SessionId, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            if (state.Rejected)
            {
                await redis.GetDatabase().KeyDeleteAsync(NativeMfaKey(ticket));
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, extensions: new Dictionary<string, object?>
                {
                    [ApiProblemExtensions.ErrorCode] = "mobile_approval_rejected"
                });
            }

            if (!state.Approved)
            {
                return Results.Accepted(value: new { status = "pending" });
            }

            await redis.GetDatabase().KeyDeleteAsync(NativeMfaKey(ticket));
            var redirectUrl = await completion.CompleteMfaWithPasskeyAsync(context, state.UserId, ct);
            return redirectUrl is null
                ? Results.Unauthorized()
                : Results.Ok(new { status = "approved", redirectUrl });
        });

        login.MapPost(IdentityApiRoutes.NativeMfaOptionsSegment, async (
            NativeMfaTicketRequest request,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion,
            IdentityDbContext db, CancellationToken ct) =>
        {
            var state = await ReadNativeMfaState(redis, request.Ticket);
            if (state is null ||
                state.Approved ||
                !completion.HasLivePendingMfaContext(state.PendingId, state.SessionId, state.UserId))
            {
                return Results.Unauthorized();
            }

            var credential = await db.PasskeyCredentials.AsNoTracking()
                .FirstOrDefaultAsync(item => item.UserId == state.UserId.ToString(), ct);
            if (credential is null)
                return Results.UnprocessableEntity(new { errorCode = "mfa_passkey_not_enrolled", message = "MFA passkey not enrolled." });

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

        login.MapPost(IdentityApiRoutes.NativeMfaRejectSegment, async (
            NativeMfaTicketRequest request,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion) =>
        {
            var state = await ReadNativeMfaState(redis, request.Ticket);
            if (state is null ||
                state.Approved ||
                state.Rejected ||
                !completion.HasLivePendingMfaContext(state.PendingId, state.SessionId, state.UserId))
            {
                return Results.Unauthorized();
            }

            var remainingLifetime = GetRemainingNativeMfaLifetime(state.CreatedAt);
            if (remainingLifetime <= TimeSpan.Zero)
            {
                await redis.GetDatabase().KeyDeleteAsync(NativeMfaKey(request.Ticket));
                return Results.Problem(statusCode: StatusCodes.Status410Gone, extensions: new Dictionary<string, object?>
                {
                    [ApiProblemExtensions.ErrorCode] = "mobile_approval_expired"
                });
            }

            state = state with { Rejected = true };
            await redis.GetDatabase().StringSetAsync(NativeMfaKey(request.Ticket), JsonSerializer.Serialize(state), remainingLifetime);
            return Results.Ok(new { rejected = true });
        });

        login.MapPost("/mfa/native/complete", async (
            NativeMfaAssertionRequest request,
            Fido2 fido2,
            IConnectionMultiplexer redis,
            OidcLoginCompletionService completion,
            IdentityDbContext db, CancellationToken ct) =>
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
                credential => credential.UserId == state.UserId.ToString() && credential.CredentialId == credentialId.ToString(), ct);
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
            state = state with { Approved = true, Rejected = false };
            await db.SaveChangesAsync(ct);
            await redisDb.StringSetAsync(
                NativeMfaKey(request.Ticket),
                JsonSerializer.Serialize(state),
                GetRemainingNativeMfaLifetime(state.CreatedAt));
            return Results.Ok(new { approved = true });
        });
    }

    private static async Task<RedisValue> GetAndDeleteAsync(IDatabase redis, RedisKey key)
    {
        var result = await redis.ScriptEvaluateAsync(
            "local value = redis.call('GET', KEYS[1]); if value then redis.call('DEL', KEYS[1]); end; return value;",
            new[] { (RedisKey)key });
        return result.IsNull ? RedisValue.Null : (RedisValue)result;
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
        if (!raw.HasValue) return null;

        var state = JsonSerializer.Deserialize<NativeMfaState>(raw!);
        if (state is null) return null;

        if (GetRemainingNativeMfaLifetime(state.CreatedAt) <= TimeSpan.Zero)
        {
            await redis.GetDatabase().KeyDeleteAsync(NativeMfaKey(ticket));
            return null;
        }

        return state;
    }

    private static TimeSpan GetRemainingNativeMfaLifetime(DateTimeOffset createdAt)
    {
        var elapsed = DateTimeOffset.UtcNow - createdAt;
        return elapsed >= NativeMfaTicketLifetime
            ? TimeSpan.Zero
            : NativeMfaTicketLifetime - elapsed;
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
        bool Rejected,
        DateTimeOffset CreatedAt);
}
