using System.Security.Cryptography.X509Certificates;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace His.Hope.IdentityService.Api.Endpoints;

/// <summary>
/// Internal identity assertion endpoint for a RADIUS outpost completing EAP-TLS.
/// The EAP exchange and RADIUS shared secret stay in the network outpost; this
/// endpoint only maps a trusted client certificate to an active Identity user.
/// </summary>
public static class RadiusEapTlsEndpoints
{
    public static void MapRadiusEapTlsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/radius/eap-tls/status", (IConfiguration configuration) =>
        {
            var caPath = configuration["Mtls:TrustedCaFile"];
            return Results.Ok(new
            {
                enabled = configuration.GetValue("Radius:EapTls:Enabled", false),
                trustedCaConfigured = !string.IsNullOrWhiteSpace(caPath),
                trustedCaReachable = !string.IsNullOrWhiteSpace(caPath) && File.Exists(caPath),
                sharedSecretManagedBy = "radius-outpost"
            });
        }).RequireAuthorization(AuthorizationConstants.Policies.HumanAdmin)
          .RequireAuthorization(AuthorizationPolicyNames.Permissions.AdminSettingsRead);

        app.MapGet("/api/v1/auth/radius/eap-tls", async (
            HttpContext http,
            IdentityDbContext db,
            UserManager<User> users,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!configuration.GetValue("Radius:EapTls:Enabled", false)) return Results.NotFound();
            var certificate = await http.Connection.GetClientCertificateAsync();
            if (certificate is null || !MtlsEndpoints.IsTrustedClientCertificate(certificate, configuration)) return Results.Unauthorized();
            var thumbprint = MtlsEndpoints.Normalize(certificate.Thumbprint);
            var binding = await db.UserClientCertificates.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Thumbprint == thumbprint && item.RevokedAt == null, ct);
            if (binding is null || binding.NotAfter <= DateTime.UtcNow) return Results.Unauthorized();
            var user = await users.FindByIdAsync(binding.UserId.ToString());
            if (user is null || !user.IsActive) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new { subject = user.Id, username = user.UserName, roles, certificateNotAfter = binding.NotAfter });
        }).AllowAnonymous();
    }
}
