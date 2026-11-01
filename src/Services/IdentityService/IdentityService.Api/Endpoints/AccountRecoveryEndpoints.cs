using System.Security.Claims;
using System.Text.Json;
using His.Hope.Contracts;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;
using His.Hope.Contracts.Identity;
using His.Hope.SharedKernel.Protocol;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class AccountRecoveryEndpoints
{
    public static RouteGroupBuilder MapAccountRecoveryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(IdentityApiRoutes.ForgotPasswordSegment, async (
            ForgotPasswordRequest request,
            IIdentityService identityService,
            IEmailSender emailSender,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Validation });

            try
            {
                var token = await identityService.GeneratePasswordResetTokenAsync(request.Email, ct);
                await emailSender.SendAsync(request.Email, "Password Reset — His.Hope",
                    $"Your password reset token: {token}", ct);
                logger.LogInformation("Password reset email dispatch completed.");
            }
            catch (KeyNotFoundException)
            {
                // Don't reveal whether email exists
            }

            return Results.Ok(new { message = "If the email exists, a reset link has been sent." });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous()
        .WithOpenApi();

        group.MapPost(IdentityApiRoutes.ResetPasswordSegment, async (
            ResetPasswordRequest request,
            IIdentityService identityService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Token) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasswordResetFieldsRequired });

            try
            {
                await identityService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, ct);
                logger.LogInformation("Password reset completed.");
                return Results.Ok(new { message = "Password has been reset successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(statusCode: 400,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidPasswordResetRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasswordResetRejected });
            }
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous()
        .WithOpenApi();

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            HttpContext httpContext,
            IIdentityService identityService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasswordFieldsRequired });

            if (request.CurrentPassword == request.NewPassword)
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasswordMustChange });

            try
            {
                await identityService.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword, ct);
                logger.LogInformation("Password changed for UserId={UserId}", userId);
                return Results.Ok(new { message = "Password changed successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasswordChangeRejected });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.PasswordChangeRejected });
            }
        })
        .RequireAuthorization()
        .RequireRateLimiting("mfa")
        .WithOpenApi();

        group.MapPost("/send-email-verification", async (
            HttpContext httpContext,
            IIdentityService identityService,
            IEmailSender emailSender,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            try
            {
                var token = await identityService.GenerateEmailConfirmationTokenAsync(userId.Value, ct);
                var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    await emailSender.SendAsync(email, "Verify Your Email — His.Hope",
                        $"Your email verification token: {token}", ct);
                    logger.LogInformation("Verification email dispatch completed.");
                }
                return Results.Ok(new { message = "Verification email sent." });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization()
        .WithOpenApi();

        group.MapPost("/verify-email", async (
            VerifyEmailRequest request,
            IIdentityService identityService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.EmailVerificationFieldsRequired });

            try
            {
                await identityService.ConfirmEmailAsync(request.Email, request.Token, ct);
                logger.LogInformation("Email verification completed.");
                return Results.Ok(new { message = "Email verified successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.InvalidEmailVerificationRequest });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: 400, detail: ex.Message,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.EmailVerificationRejected });
            }
        })
        .AllowAnonymous()
        .WithOpenApi();

        group.MapGet("/sessions", async (
            HttpContext httpContext,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            var currentSessionId = httpContext.Request.Cookies[HisHopeProtocolConstants.Cookies.BrowserSession];
            var sessionIds = await sessionTracker.GetUserSessionsAsync(userId.Value.ToString());
            var db = redis.GetDatabase();
            var sessions = new List<SessionInfo>();

            foreach (var sid in sessionIds)
            {
                var data = await db.StringGetAsync($"session:{sid}");
                SessionInfo info;
                if (data.HasValue)
                {
                    var session = JsonSerializer.Deserialize<SessionData>(data!);
                    if (session is not null)
                    {
                        info = new SessionInfo(
                            sid,
                            session.UserAgentHash?[..Math.Min(20, session.UserAgentHash.Length)] + "...",
                            null,
                            session.IssuedAt,
                            session.ExpiresAt,
                            sid == currentSessionId);
                    }
                    else
                    {
                        info = new SessionInfo(sid, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, sid == currentSessionId);
                    }
                }
                else
                {
                    info = new SessionInfo(sid, null, null, DateTimeOffset.MinValue, DateTimeOffset.MinValue, sid == currentSessionId);
                }
                sessions.Add(info);
            }

            return Results.Ok(new { sessions });
        })
        .RequireAuthorization()
        .WithOpenApi();

        group.MapDelete("/sessions/{sessionId}", async (
            string sessionId,
            HttpContext httpContext,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            var currentSessionId = httpContext.Request.Cookies[HisHopeProtocolConstants.Cookies.BrowserSession];
            if (sessionId == currentSessionId)
                return Results.Problem(statusCode: 400,
                    extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.CurrentSessionCannotBeRevoked });

            var db = redis.GetDatabase();
            await db.KeyDeleteAsync($"session:{sessionId}");
            await sessionTracker.RemoveSessionAsync(userId.Value.ToString(), sessionId);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithOpenApi();

        group.MapDelete("/sessions", async (
            HttpContext httpContext,
            IUserSessionTracker sessionTracker,
            IConnectionMultiplexer redis) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            var currentSessionId = httpContext.Request.Cookies[HisHopeProtocolConstants.Cookies.BrowserSession];
            var sessionIds = await sessionTracker.GetUserSessionsAsync(userId.Value.ToString());
            var db = redis.GetDatabase();
            var keys = sessionIds
                .Where(s => s != currentSessionId)
                .Select(s => (RedisKey)$"session:{s}")
                .ToArray();

            if (keys.Length > 0)
                await db.KeyDeleteAsync(keys);

            await sessionTracker.ClearUserSessionsAsync(userId.Value.ToString());

            if (currentSessionId is not null)
                await sessionTracker.AddSessionAsync(userId.Value.ToString(), currentSessionId);

            return Results.Ok(new { revoked = keys.Length });
        })
        .RequireAuthorization()
        .WithOpenApi();

        return group;
    }

    private static Guid? GetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirst(His.Hope.SharedKernel.Protocol.HisHopeProtocolConstants.Claims.Subject);
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
