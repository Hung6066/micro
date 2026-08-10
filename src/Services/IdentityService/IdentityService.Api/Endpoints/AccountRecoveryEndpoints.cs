using System.Security.Claims;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class AccountRecoveryEndpoints
{
    public static RouteGroupBuilder MapAccountRecoveryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            IIdentityService identityService,
            IEmailSender emailSender,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.Problem("Email is required.", statusCode: 400);

            try
            {
                var token = await identityService.GeneratePasswordResetTokenAsync(request.Email);
                await emailSender.SendAsync(request.Email, "Password Reset — His.Hope",
                    $"Your password reset token: {token}", ct);
                logger.LogInformation("Password reset email sent to {Email}", request.Email);
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

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            IIdentityService identityService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Token) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
                return Results.Problem("Email, token, and new password are required.", statusCode: 400);

            try
            {
                await identityService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
                logger.LogInformation("Password reset completed for {Email}", request.Email);
                return Results.Ok(new { message = "Password has been reset successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem("Invalid reset request.", statusCode: 400);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
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
                return Results.Problem("Current password and new password are required.", statusCode: 400);

            if (request.CurrentPassword == request.NewPassword)
                return Results.Problem("New password must differ from current password.", statusCode: 400);

            try
            {
                await identityService.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);
                logger.LogInformation("Password changed for UserId={UserId}", userId);
                return Results.Ok(new { message = "Password changed successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
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
                var token = await identityService.GenerateEmailConfirmationTokenAsync(userId.Value);
                var email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value;
                if (!string.IsNullOrEmpty(email))
                {
                    await emailSender.SendAsync(email, "Verify Your Email — His.Hope",
                        $"Your email verification token: {token}", ct);
                    logger.LogInformation("Verification email sent to {Email}", email);
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
                return Results.Problem("Email and token are required.", statusCode: 400);

            try
            {
                await identityService.ConfirmEmailAsync(request.Email, request.Token);
                logger.LogInformation("Email verified for {Email}", request.Email);
                return Results.Ok(new { message = "Email verified successfully." });
            }
            catch (KeyNotFoundException)
            {
                return Results.Problem("Invalid verification request.", statusCode: 400);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
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

            var currentSessionId = httpContext.Request.Cookies["hishop_sid"];
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

            var currentSessionId = httpContext.Request.Cookies["hishop_sid"];
            if (sessionId == currentSessionId)
                return Results.Problem("Cannot revoke current session. Use /logout instead.", statusCode: 400);

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

            var currentSessionId = httpContext.Request.Cookies["hishop_sid"];
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
                    ?? httpContext.User.FindFirst("sub");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
