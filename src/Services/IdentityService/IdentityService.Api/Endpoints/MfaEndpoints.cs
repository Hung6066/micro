using System.Security.Claims;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class MfaEndpoints
{
    public static RouteGroupBuilder MapMfaEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/mfa/enroll", async (
            HttpContext httpContext,
            TotpService totpService,
            RecoveryCodeService recoveryCodeService,
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
            var qrUri = totpService.GenerateQrCodeUri(secret, user.Email!);
            var rawCodes = recoveryCodeService.GenerateCodes(8);
            var hashedCodes = rawCodes.Select(recoveryCodeService.HashCode).ToArray();

            if (existing is null)
            {
                db.UserMfas.Add(new UserMfa
                {
                    UserId = userId.Value,
                    SecretKey = secret,
                    RecoveryCodes = hashedCodes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.SecretKey = secret;
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

            if (!totpService.VerifyCode(mfa.SecretKey, request.Code))
                return Results.Problem("Invalid TOTP code.", statusCode: 400);

            mfa.IsEnabled = true;
            mfa.EnrolledAt = DateTime.UtcNow;
            mfa.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var roles = await userManager.GetRolesAsync(user);
            var permissions = await GetPermissionsForRoles(roles, db, ct);
            var (accessToken, expiresAt) = tokenGenerator.GenerateAccessToken(
                user, roles, permissions, amrValues: ["pwd", "otp"]);

            var refreshToken = tokenGenerator.GenerateRefreshToken();

            return Results.Ok(new MfaVerifyResponse(
                accessToken, refreshToken, expiresAt,
                MapToDto(user, roles)));
        })
        .RequireAuthorization()
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
}
