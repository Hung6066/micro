using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class MfaEndpoints
{
    public static RouteGroupBuilder MapMfaEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/mfa/enroll", async (
            HttpContext httpContext,
            TotpService totpService,
            RecoveryCodeService recoveryCodeService,
            IMfaSecretEncryptor encryptor,
            IdentityDbContext db,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId.Value.ToString());
            if (user is null) return Results.NotFound();

            var existing = await db.UserMfas
                .FirstOrDefaultAsync(m => m.UserId == userId.Value, ct);

            if (existing is { IsEnabled: true })
                return Results.Problem("MFA is already enabled.", statusCode: 400);

            var secret = totpService.GenerateSecret();
            var encryptedSecret = encryptor.Encrypt(secret);
            var qrUri = totpService.GenerateQrCodeUri(secret, user.Email!);
            var rawCodes = recoveryCodeService.GenerateCodes(8);
            var hashedCodes = rawCodes.Select(recoveryCodeService.HashCode).ToArray();

            if (existing is null)
            {
                db.UserMfas.Add(new UserMfa
                {
                    UserId = userId.Value,
                    SecretKey = encryptedSecret,
                    RecoveryCodes = hashedCodes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.SecretKey = encryptedSecret;
                existing.RecoveryCodes = hashedCodes;
                existing.BackupCodesUsed = 0;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(new MfaEnrollResponse(secret, qrUri, rawCodes));
        })
        .RequireAuthorization()
        .WithOpenApi();

        group.MapPost("/mfa/verify", async (
            MfaVerifyRequest request,
            HttpContext httpContext,
            TotpService totpService,
            JwtTokenGenerator tokenGenerator,
            IMfaSecretEncryptor encryptor,
            IConnectionMultiplexer redis,
            IdentityDbContext db,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId.Value.ToString());
            if (user is null) return Results.NotFound();

            var mfa = await db.UserMfas
                .FirstOrDefaultAsync(m => m.UserId == userId.Value, ct);

            if (mfa is null)
                return Results.Problem("MFA not enrolled. Enroll first.", statusCode: 400);

            var decryptedSecret = encryptor.Decrypt(mfa.SecretKey);
            if (!totpService.VerifyCode(decryptedSecret, request.Code))
                return Results.Problem("Invalid TOTP code.", statusCode: 400);

            mfa.IsEnabled = true;
            mfa.EnrolledAt = DateTime.UtcNow;
            mfa.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var roles = await userManager.GetRolesAsync(user);
            var permissions = await GetPermissionsForRoles(roles, db, ct);
            var (accessToken, expiresAt) = tokenGenerator.GenerateAccessToken(
                user, roles, permissions, amrValues: ["pwd", "otp"]);

            var refreshTokenValue = tokenGenerator.GenerateRefreshToken();

            // SECURITY: BFF mode — store tokens in HttpOnly cookie session, not response body
            var sessionId = Guid.NewGuid().ToString("N");
            var csrfToken = Guid.NewGuid().ToString("N");
            var sessionData = new SessionData
            {
                UserId = user.Id.ToString(),
                Jwt = accessToken,
                RefreshToken = refreshTokenValue,
                Permissions = permissions.ToArray(),
                CsrfToken = csrfToken,
                UserAgentHash = ComputeSha256(httpContext.Request.Headers.UserAgent.ToString()),
                IssuedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt
            };

            var rdb = redis.GetDatabase();
            await rdb.StringSetAsync(
                $"session:{sessionId}",
                JsonSerializer.Serialize(sessionData),
                TimeSpan.FromHours(1));

            httpContext.Response.Cookies.Append("hishop_sid", sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api",
                MaxAge = TimeSpan.FromHours(1)
            });

            httpContext.Response.Cookies.Append("hishop_csrf", csrfToken, new CookieOptions
            {
                HttpOnly = false,
                Secure = httpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                MaxAge = TimeSpan.FromHours(1)
            });

            return Results.Ok(new { status = "ok", userId = user.Id, requiresMfa = false });
        })
        .RequireAuthorization()
        .RequireRateLimiting("mfa")
        .WithOpenApi();

        group.MapPost("/mfa/recover", async (
            MfaRecoverRequest request,
            HttpContext httpContext,
            RecoveryCodeService recoveryCodeService,
            TotpService totpService,
            IdentityDbContext db,
            UserManager<User> userManager,
            CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null) return Results.Unauthorized();

            var user = await userManager.FindByIdAsync(userId.Value.ToString());
            if (user is null) return Results.NotFound();

            var mfa = await db.UserMfas
                .FirstOrDefaultAsync(m => m.UserId == userId.Value, ct);

            if (mfa is null)
                return Results.Problem("MFA not enrolled.", statusCode: 400);

            var codeHash = recoveryCodeService.HashCode(request.RecoveryCode);
            var index = Array.IndexOf(mfa.RecoveryCodes, codeHash);

            if (index < 0)
                return Results.Problem("Invalid recovery code.", statusCode: 400);

            var codes = mfa.RecoveryCodes.ToList();
            codes.RemoveAt(index);
            mfa.RecoveryCodes = [.. codes];
            mfa.BackupCodesUsed++;
            mfa.IsEnabled = false;
            mfa.EnrolledAt = null;
            mfa.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { message = "MFA has been reset. Re-enroll to set up a new authenticator." });
        })
        .RequireAuthorization()
        .RequireRateLimiting("mfa")
        .WithOpenApi();

        return group;
    }

    private static Guid? GetUserId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirst("sub");
        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    private static async Task<List<string>> GetPermissionsForRoles(
        IList<string> roleNames, IdentityDbContext db, CancellationToken ct)
    {
        var roleIds = await db.Roles
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (roleIds.Count == 0) return [];

        return await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync(ct);
    }

    private static UserDto MapToDto(User user, IList<string> roles) => new(
        user.Id, user.UserName!, user.Email!,
        user.FirstName, user.LastName, user.MiddleName,
        user.FullName, user.LicenseNumber, user.Specialty, roles);

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
