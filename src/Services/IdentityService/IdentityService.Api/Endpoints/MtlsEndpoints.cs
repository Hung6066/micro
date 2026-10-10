using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Domain.Common;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.IdentityService.Api.Services;
using His.Hope.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

public static class MtlsEndpoints
{
    public static void MapMtlsEndpoints(this WebApplication app)
    {
        app.MapGet(IdentityApiRoutes.MtlsLogin, async (
            HttpContext http,
            IdentityDbContext db,
            UserManager<User> users,
            OidcLoginCompletionService completion,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            var certificate = await http.Connection.GetClientCertificateAsync();
            if (certificate is null || !IsTrustedClientCertificate(certificate, configuration))
                return Results.Unauthorized();

            var thumbprint = Normalize(certificate.Thumbprint);
            var binding = await db.UserClientCertificates.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Thumbprint == thumbprint && item.RevokedAt == null, ct);
            if (binding is null || binding.NotAfter <= DateTime.UtcNow)
                return Results.Unauthorized();

            var user = await users.FindByIdAsync(binding.UserId.ToString());
            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var result = await completion.CompletePrimaryAsync(http, user, "/", ["mtls"], ct);
            return Results.Redirect(result.RedirectUrl);
        }).AllowAnonymous();

        var admin = app.MapGroup(IdentityApiRoutes.AdminMtls)
            .RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
            .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsRead);
        admin.MapGet("/bindings", async (IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var query = db.UserClientCertificates.AsNoTracking();
            var allowedFacilities = GetAllowedFacilities(facilityContext);
            if (!facilityContext.IsCrossFacility && allowedFacilities.Length > 0)
            {
                query = query.Where(binding => db.UserFacilities.Any(membership =>
                    membership.UserId == binding.UserId && membership.IsActive && membership.RevokedAt == null &&
                    allowedFacilities.Contains(membership.FacilityId)));
            }

            var bindings = await query
                .OrderByDescending(item => item.CreatedAt)
                .Take(200)
                .Select(item => new
                {
                    item.Id,
                    item.UserId,
                    thumbprint = item.Thumbprint,
                    item.Subject,
                    item.NotAfter,
                    item.RevokedAt,
                    status = item.RevokedAt != null ? "revoked" : item.NotAfter <= DateTime.UtcNow ? "expired" : "active"
                })
                .ToListAsync(ct);
            return Results.Ok(bindings);
        });
        admin.MapPost("/bindings", async (MtlsBindingRequest request, IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            if (!Guid.TryParse(request.UserId, out var userId) || string.IsNullOrWhiteSpace(request.Thumbprint))
                return Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { [ApiProblemExtensions.ErrorCode] = ApiErrorCodes.Validation });
            // Do not rely on the FK to reject unknown identities: that would surface as
            // a 500 and leak persistence details instead of a policy decision.
            if (!await db.Users.AnyAsync(user => user.Id == userId, ct)) return Results.Forbid();
            if (!await HasFacilityAccessAsync(db, facilityContext, userId, ct)) return Results.Forbid();
            var thumbprint = Normalize(request.Thumbprint);
            if (await db.UserClientCertificates.AnyAsync(item => item.Thumbprint == thumbprint && item.RevokedAt == null, ct))
                return Results.Conflict();
            var binding = new UserClientCertificate
            {
                UserId = userId,
                Thumbprint = thumbprint,
                Subject = request.Subject,
                NotAfter = request.NotAfter
            };
            db.UserClientCertificates.Add(binding);
            await db.SaveChangesAsync(ct);
            return Results.Created($"{IdentityApiRoutes.AdminMtlsBindings}/{binding.Id}", new { binding.Id, binding.Thumbprint, binding.NotAfter });
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite);
        admin.MapDelete("/bindings/{id:guid}", async (Guid id, IdentityDbContext db, FacilityContext facilityContext, CancellationToken ct) =>
        {
            var binding = await db.UserClientCertificates.SingleOrDefaultAsync(item => item.Id == id, ct);
            binding = Guard.Against.NotFound(binding, "UserClientCertificate", id);
            if (!await HasFacilityAccessAsync(db, facilityContext, binding.UserId, ct)) return Results.Forbid();
            binding.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminClientsWrite);
    }

    internal static string Normalize(string? thumbprint) =>
            string.IsNullOrWhiteSpace(thumbprint) ? string.Empty : thumbprint.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static async Task<bool> HasFacilityAccessAsync(IdentityDbContext db, FacilityContext context, Guid userId, CancellationToken ct)
    {
        if (context.IsCrossFacility) return true;
        var facilities = GetAllowedFacilities(context);
        if (facilities.Length == 0) return true;
        return await db.UserFacilities.AnyAsync(membership => membership.UserId == userId && membership.IsActive &&
            membership.RevokedAt == null && facilities.Contains(membership.FacilityId), ct);
    }

    private static string[] GetAllowedFacilities(FacilityContext context) => context.AuthorizedFacilities
        .Append(context.FacilityId)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    internal static bool IsTrustedClientCertificate(X509Certificate2 certificate, IConfiguration configuration)
    {
        if (DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() || DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
            return false;
        var eku = certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
        if (eku is not null && !eku.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == "1.3.6.1.5.5.7.3.2"))
            return false;

        var caPath = configuration["Mtls:TrustedCaFile"];
        if (string.IsNullOrWhiteSpace(caPath) || !File.Exists(caPath))
            return certificate.Verify();

        using var ca = new X509Certificate2(caPath);
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(ca);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        return chain.Build(certificate);
    }

    public sealed record MtlsBindingRequest(string UserId, string Thumbprint, DateTime NotAfter, string? Subject = null);
}
