using His.Hope.AspNetCore;
using His.Hope.Validation;
using His.Hope.ServiceDefaults;
using His.Hope.Observability;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using His.Hope.Bff.Core.Authentication;
using His.Hope.IdentityService.Api.Endpoints;
using His.Hope.IdentityService.Api.Jobs;
using His.Hope.IdentityService.Api.Services;
using His.Hope.SharedKernel.Authorization;
using His.Hope.IdentityService.Api.Configuration;
using His.Hope.IdentityService.Application;
using His.Hope.IdentityService.Application.DTOs;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Application.Services;
using His.Hope.IdentityService.Infrastructure.Services;
using His.Hope.IdentityService.Infrastructure.Facility;
using His.Hope.Persistence;
using His.Hope.Infrastructure;
using His.Hope.Infrastructure.Audit;
using His.Hope.Infrastructure.Caching;
using His.Hope.Infrastructure.Contracts;
using His.Hope.Infrastructure.Middleware;
using His.Hope.IdentityService.Api.Middleware;
using His.Hope.Infrastructure.Observability;
using His.Hope.Infrastructure.Locking;
using His.Hope.Infrastructure.Security;
using His.Hope.Authorization;
using MediatR;
using OpenIddictEntityFrameworkCore = OpenIddict.EntityFrameworkCore.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using His.Hope.AspNetCore.ProblemDetails;
using Serilog;
using StackExchange.Redis;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

namespace His.Hope.IdentityService.Api.Composition;

public static class IdentityServicePipelineExtensions
{
    public static void UseIdentityServicePipeline(this WebApplication app)
    {

        if (app.Environment.IsProduction())
            app.Services.RequireDurableAuditSink();

        // Keep unexpected API failures in the same RFC 7807 shape consumed by Angular.
        app.UseHisHopeExceptionHandler();
        app.UseStatusCodePages(async statusContext =>
        {
            var http = statusContext.HttpContext;
            var status = http.Response.StatusCode;
            // Preserve the RFC 6749/OIDC error payload produced by OpenIddict.
            // Replacing it with the generic API ProblemDetails hides the exact
            // validation reason needed by OIDC clients and operators.
            if (http.Request.Path.StartsWithSegments("/connect") ||
                http.Request.Path.StartsWithSegments("/.well-known"))
                return;
            if (http.Response.HasStarted || http.Response.ContentLength is not null ||
                status is not (400 or 401 or 403 or 404 or 409 or 429))
                return;

            var correlationId = http.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? http.TraceIdentifier;
            var problem = new ProblemDetails
            {
                Status = status,
                Title = status switch
                {
                    400 => "The request is invalid.",
                    401 => "Authentication is required.",
                    403 => "The current user is not allowed to perform this action.",
                    404 => "The requested resource was not found.",
                    409 => "The request conflicts with the current resource state.",
                    429 => "Too many requests.",
                    _ => "The request failed."
                },
                Instance = http.Request.Path
            };
            problem.Extensions[ApiProblemExtensions.CorrelationId] = correlationId;
            problem.Extensions[ApiProblemExtensions.ErrorCode] = ApiErrorCodes.ForStatus(status);
            http.Response.ContentType = "application/problem+json";
            await http.Response.WriteAsJsonAsync(problem);
        });

        app.UseHisHopeServiceDefaults();
        app.UseStaticFiles();
        app.UseGlobalExceptionHandler();

        // SECURITY: Seed identity database with permissions, roles, and admin user
        His.Hope.IdentityService.Infrastructure.Persistence.IdentityDbInitializer.Initialize(
            app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseSecurityHeaders();
        app.UseRateLimiting();
        app.UseMiddleware<His.Hope.IdentityService.Api.Metrics.SloMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseHisHopePrometheus();
        app.UseRouting();
        app.UseCors();
        app.UseRateLimiter();
        app.UseDpopAuthorizationSchemeNormalization();

        // BFF-only browser sessions keep the access token server-side in Redis.
        // API calls arriving through the shared parent-domain cookie therefore
        // need the same session bridge as the gateway/BFFs before authentication
        // selects the bearer policy. Never override an explicit Authorization
        // header, and never accept an unprotected/expired session payload.
        app.Use(async (context, next) =>
        {
            if (!context.Request.Headers.ContainsKey("Authorization") &&
                context.Request.Cookies.TryGetValue("hishop_sid", out var sessionId) &&
                !string.IsNullOrWhiteSpace(sessionId) &&
                !context.Request.Path.StartsWithSegments("/connect") &&
                !context.Request.Path.StartsWithSegments("/Account") &&
                !context.Request.Path.StartsWithSegments("/api/v1/auth/session-status") &&
                !context.Request.Path.StartsWithSegments("/api/v1/auth/session/exchange"))
            {
                var redis = context.RequestServices.GetRequiredService<StackExchange.Redis.IConnectionMultiplexer>();
                var protector = context.RequestServices.GetRequiredService<SessionTokenProtector>();
                var sessionJson = await redis.GetDatabase().StringGetAsync($"session:{sessionId}");
                if (sessionJson.HasValue)
                {
                    try
                    {
                        using var document = JsonDocument.Parse((string)sessionJson!);
                        var root = document.RootElement;
                        var protectedJwt = root.TryGetProperty("Jwt", out var jwtElement)
                            ? jwtElement.GetString()
                            : null;
                        var expiresAt = root.TryGetProperty("ExpiresAt", out var expiryElement)
                            ? expiryElement.GetDateTimeOffset()
                            : DateTimeOffset.MinValue;
                        var idleExpiresAt = root.TryGetProperty("IdleExpiresAt", out var idleElement) &&
                            idleElement.ValueKind != JsonValueKind.Null
                            ? idleElement.GetDateTimeOffset()
                            : DateTimeOffset.MaxValue;
                        var absoluteExpiresAt = root.TryGetProperty("AbsoluteExpiresAt", out var absoluteElement) &&
                            absoluteElement.ValueKind != JsonValueKind.Null
                            ? absoluteElement.GetDateTimeOffset()
                            : DateTimeOffset.MaxValue;
                        var sessionPrincipalType = root.TryGetProperty("PrincipalType", out var principalTypeElement)
                            ? principalTypeElement.GetString()
                            : null;
                        if (!string.IsNullOrWhiteSpace(protectedJwt) &&
                            expiresAt > DateTimeOffset.UtcNow &&
                            idleExpiresAt > DateTimeOffset.UtcNow &&
                            absoluteExpiresAt > DateTimeOffset.UtcNow)
                        {
                            var jwt = protector.Unprotect(protectedJwt);

                            // Migrate legacy/stale BFF sessions. A session can
                            // carry a valid human marker while its permission
                            // snapshot is empty or obsolete; that state makes
                            // every HumanAdmin permission policy return 403.
                            var tokenGenerator = context.RequestServices.GetRequiredService<JwtTokenGenerator>();
                            ClaimsPrincipal? tokenPrincipal = null;
                            try
                            {
                                tokenPrincipal = tokenGenerator.GetPrincipalFromExpiredToken(jwt);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                // Preserve the existing fail-closed behavior
                                // for invalid or legacy session payloads.
                            }
                            var sessionData = JsonSerializer.Deserialize<SessionData>((string)sessionJson!);
                            var hasHumanPrincipal = string.Equals(sessionPrincipalType, AuthorizationConstants.PrincipalTypes.Human, StringComparison.Ordinal) ||
                                string.Equals(tokenPrincipal?.FindFirst(AuthorizationConstants.Claims.PrincipalType)?.Value, AuthorizationConstants.PrincipalTypes.Human, StringComparison.Ordinal);
                            var hasPermissionClaims = tokenPrincipal?.FindAll("permissions")
                                .SelectMany(claim => claim.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                                .Any() == true;
                            if ((!hasHumanPrincipal || !hasPermissionClaims) &&
                                tokenPrincipal is not null &&
                                Guid.TryParse(root.GetProperty("UserId").GetString(), out var sessionUserId))
                            {
                                var userManager = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<User>>();
                                var identityService = context.RequestServices.GetRequiredService<IIdentityService>();
                                var user = await userManager.FindByIdAsync(sessionUserId.ToString());
                                if (user is not null)
                                {
                                    var roles = await userManager.GetRolesAsync(user);
                                    var (permissions, tenantClaims) = await HumanSessionAuthClaims.ResolveAsync(
                                        userManager,
                                        identityService,
                                        user);
                                    var permissionList = permissions.ToList();
                                    var (migratedJwt, migratedExpiry) = tokenGenerator.GenerateAccessToken(
                                        user,
                                        roles,
                                        permissionList,
                                        additionalClaims: tenantClaims);
                                    jwt = migratedJwt;
                                    if (sessionData is not null)
                                    {
                                        sessionData = sessionData with
                                        {
                                            Jwt = protector.Protect(migratedJwt),
                                            PrincipalType = AuthorizationConstants.PrincipalTypes.Human,
                                            Permissions = permissionList.ToArray(),
                                            ExpiresAt = migratedExpiry < sessionData.ExpiresAt ? migratedExpiry : sessionData.ExpiresAt
                                        };
                                        await redis.GetDatabase().StringSetAsync(
                                            $"session:{sessionId}",
                                            JsonSerializer.Serialize(sessionData),
                                            sessionData.ExpiresAt - DateTimeOffset.UtcNow);
                                    }
                                }
                            }

                            context.Request.Headers.Authorization = $"Bearer {jwt}";
                            context.Request.Headers["X-HisHope-Session"] = "1";
                        }
                    }
                    catch (CryptographicException)
                    {
                        // Treat an invalid session as anonymous; the normal
                        // authentication middleware returns the RFC 401/403.
                    }
                    catch (JsonException)
                    {
                        // Treat malformed Redis data as an anonymous request.
                    }
                }
            }

            await next();
        });

        app.UseAuthentication();
        app.UseDpopAccessTokenValidation();
        app.UseMiddleware<SecurityVersionMiddleware>();

        // Facility resolution: extracts facility_id from JWT, sets FacilityContext (before authorization)
        app.UseFacilityResolution();

        app.UseAuthorization();
        app.UseAssurancePolicyEnforcement();
        app.MapControllers();
        app.UsePhiAudit();

        // Auth endpoints
    }
}
