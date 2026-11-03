using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Testing;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class PlatformAdminSessionTests(IdentityServiceTestFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Configured_super_admin_uses_seeded_hq_membership_for_client_sessions(
        bool restrictToControlPlane)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await users.FindByIdAsync(IdentityTestData.AdminId.ToString())
            ?? throw new InvalidOperationException("The test administrator is missing.");
        var memberships = (await users.GetClaimsAsync(admin))
            .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership)
            .ToArray();
        Assert.True((await users.RemoveClaimsAsync(admin, memberships)).Succeeded);
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var previousRestriction = configuration["Identity:SuperAdmin:RestrictToControlPlane"];
        var previousSeed = configuration["Identity:SuperAdmin:SeedHqMembership"];
        var previousMigrations = configuration["Persistence:RunMigrationsOnStartup"];
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var previousMfa = await db.Set<UserMfa>().AnyAsync(item => item.UserId == admin.Id);
        if (!previousMfa)
        {
            db.Set<UserMfa>().Add(new UserMfa { UserId = admin.Id, IsEnabled = true, SecretKey = "integration-only" });
            await db.SaveChangesAsync();
        }
        configuration["Identity:SuperAdmin:RestrictToControlPlane"] = restrictToControlPlane.ToString();
        try
        {
            using var deniedRequest = await ExchangeRequestAsync(scope.ServiceProvider, admin, "his-hope-admin");
            using var denied = await fixture.AnonymousClient.SendAsync(deniedRequest);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            configuration["Identity:SuperAdmin:SeedHqMembership"] = "true";
            configuration["Persistence:RunMigrationsOnStartup"] = "false";
            IdentityDbInitializer.Initialize(fixture.Services);
            IdentityDbInitializer.Initialize(fixture.Services);
            await db.Entry(admin).ReloadAsync();
            var seeded = (await users.GetClaimsAsync(admin))
                .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership).ToArray();
            Assert.Single(seeded);
            Assert.Equal("nacoms-group", seeded[0].Value);

            using var request = await ExchangeRequestAsync(scope.ServiceProvider, admin, "his-hope-admin");
            using var response = await fixture.AnonymousClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            using var session = fixture.CreateSessionClient();
            session.ApplySetCookieHeaders(response);
            using var clients = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminClients);
            Assert.Equal(HttpStatusCode.OK, clients.StatusCode);
            var clientId = $"platform-session-{Guid.NewGuid():N}";
            using var create = await session.PostWithCookiesAsync(IdentityApiRoutes.AdminClients, new
            {
                clientId,
                displayName = "Platform session regression",
                type = "confidential",
                grantTypes = new[] { "authorization_code" },
                redirectUris = new[] { "https://platform.example/callback" },
                scopes = new[] { "openid" }
            });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            using var updatedList = await session.GetWithCookiesAsync(
                $"{IdentityApiRoutes.AdminClients}?search={clientId}");
            Assert.Equal(HttpStatusCode.OK, updatedList.StatusCode);
            using var created = JsonDocument.Parse(await updatedList.Content.ReadAsStringAsync());
            var id = created.RootElement.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("clientId").GetString() == clientId)
                .GetProperty("id").GetString();
            using var delete = await session.DeleteWithCookiesAsync($"{IdentityApiRoutes.AdminClients}/{id}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            using var otherPortal = await ExchangeRequestAsync(scope.ServiceProvider, admin, "integration-tenant-portal");
            using var otherResponse = await fixture.AnonymousClient.SendAsync(otherPortal);
            Assert.Equal(HttpStatusCode.Forbidden, otherResponse.StatusCode);

            var hq = await db.IamScopes.SingleAsync(item => item.Kind == "tenant" && item.Key == "nacoms-group");
            var hqId = hq.Id;
            hq.Key = "group-hq";
            await db.SaveChangesAsync();
            Assert.True((await users.RemoveClaimsAsync(admin, seeded)).Succeeded);
            Assert.True((await users.AddClaimAsync(admin, new Claim(
                HisHopeProtocolConstants.Claims.TenantMembership, "group-hq"))).Succeeded);
            IdentityDbInitializer.Initialize(fixture.Services);
            await db.Entry(admin).ReloadAsync();
            await db.Entry(hq).ReloadAsync();
            Assert.Equal(hqId, hq.Id);
            Assert.Equal("nacoms-group", hq.Key);
            foreach (var claimEntry in db.ChangeTracker.Entries<IdentityUserClaim<Guid>>().ToArray())
                await claimEntry.ReloadAsync();
            var migrated = (await users.GetClaimsAsync(admin))
                .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership).ToArray();
            Assert.Single(migrated);
            Assert.Equal("nacoms-group", migrated[0].Value);
        }
        finally
        {
            configuration["Identity:SuperAdmin:RestrictToControlPlane"] = previousRestriction;
            configuration["Identity:SuperAdmin:SeedHqMembership"] = previousSeed;
            configuration["Persistence:RunMigrationsOnStartup"] = previousMigrations;
            var seeded = (await users.GetClaimsAsync(admin))
                .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership).ToArray();
            Assert.True((await users.RemoveClaimsAsync(admin, seeded)).Succeeded);
            Assert.True((await users.AddClaimsAsync(admin, memberships)).Succeeded);
            if (!previousMfa)
            {
                await db.Set<UserMfa>().Where(item => item.UserId == admin.Id).ExecuteDeleteAsync();
            }
        }
    }

    [Fact]
    public async Task Admin_client_requires_hq_membership_and_rejection_preserves_existing_session()
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = new User
        {
            UserName = $"tenant-admin-{Guid.NewGuid():N}",
            Email = $"tenant-admin-{Guid.NewGuid():N}@example.test",
            EmailConfirmed = true
        };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var oldSessionId = Guid.NewGuid().ToString("N");
        var oldSessionKey = $"session:{oldSessionId}";
        await redis.StringSetAsync(oldSessionKey, "existing-session", TimeSpan.FromMinutes(5));
        try
        {
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
            Assert.True((await users.AddClaimAsync(user,
                new Claim(HisHopeProtocolConstants.Claims.TenantMembership, "manufacturing"))).Succeeded);
            using var request = await ExchangeRequestAsync(scope.ServiceProvider, user, "his-hope-admin");
            var identityCookie = request.Headers.GetValues("Cookie").Single();
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", $"{identityCookie}; hishop_sid={oldSessionId}");
            using var response = await fixture.AnonymousClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
            Assert.True(await redis.KeyExistsAsync(oldSessionKey));

            Assert.True((await users.AddClaimAsync(user,
                new Claim(HisHopeProtocolConstants.Claims.TenantMembership, "nacoms-group"))).Succeeded);
            using var hqRequest = await ExchangeRequestAsync(scope.ServiceProvider, user, "his-hope-admin");
            using var hqResponse = await fixture.AnonymousClient.SendAsync(hqRequest);
            Assert.Equal(HttpStatusCode.NoContent, hqResponse.StatusCode);

            using var tenantRequest = await ExchangeRequestAsync(scope.ServiceProvider, user, "integration-hq-portal");
            using var tenantResponse = await fixture.AnonymousClient.SendAsync(tenantRequest);
            Assert.Equal(HttpStatusCode.NoContent, tenantResponse.StatusCode);
        }
        finally
        {
            await redis.KeyDeleteAsync(oldSessionKey);
            Assert.True((await users.DeleteAsync(user)).Succeeded);
        }
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public async Task Hq_seed_rejects_unqualified_configured_users(
        bool active, bool confirmed, bool mfa, bool adminRole)
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var user = new User
        {
            UserName = $"hq-seed-{Guid.NewGuid():N}",
            Email = $"hq-seed-{Guid.NewGuid():N}@example.test",
            EmailConfirmed = confirmed,
            IsActive = active
        };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        if (adminRole) Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
        if (mfa)
        {
            db.Set<UserMfa>().Add(new UserMfa { UserId = user.Id, IsEnabled = true, SecretKey = "integration-only" });
            await db.SaveChangesAsync();
        }
        var previousId = configuration["Identity:SuperAdmin:UserIds:0"];
        var previousSeed = configuration["Identity:SuperAdmin:SeedHqMembership"];
        var previousMigrations = configuration["Persistence:RunMigrationsOnStartup"];
        try
        {
            configuration["Identity:SuperAdmin:UserIds:0"] = user.Id.ToString();
            configuration["Identity:SuperAdmin:SeedHqMembership"] = "true";
            configuration["Persistence:RunMigrationsOnStartup"] = "false";
            Assert.Throws<InvalidOperationException>(() => IdentityDbInitializer.Initialize(fixture.Services));
            Assert.Empty((await users.GetClaimsAsync(user))
                .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership));
        }
        finally
        {
            configuration["Identity:SuperAdmin:UserIds:0"] = previousId;
            configuration["Identity:SuperAdmin:SeedHqMembership"] = previousSeed;
            configuration["Persistence:RunMigrationsOnStartup"] = previousMigrations;
            Assert.True((await users.DeleteAsync(user)).Succeeded);
        }
    }

    [Fact]
    public async Task Platform_admin_cannot_exchange_for_a_tenant_portal_without_membership()
    {
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await users.FindByIdAsync(IdentityTestData.AdminId.ToString())
            ?? throw new InvalidOperationException("The test administrator is missing.");
        using var request = await ExchangeRequestAsync(scope.ServiceProvider, admin, "integration-tenant-portal");
        using var response = await fixture.AnonymousClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Platform_client_exchange_without_identity_cookie_is_unauthorized()
    {
        using var response = await fixture.AnonymousClient.PostAsJsonAsync(
            IdentityApiRoutes.SessionExchange, new { clientId = "his-hope-admin" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpRequestMessage> ExchangeRequestAsync(
        IServiceProvider services, User user, string clientId)
    {
        var signIn = services.GetRequiredService<SignInManager<User>>();
        var principal = await signIn.CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(AuthorizationConstants.Claims.PrincipalType,
            AuthorizationConstants.PrincipalTypes.Human));
        identity.AddClaim(new Claim(HisHopeProtocolConstants.Claims.AuthenticationMethod, "totp"));
        identity.AddClaim(new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ClaimValueTypes.Integer64));
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5)
        }, IdentityConstants.ApplicationScheme);
        var request = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.SessionExchange)
        {
            Content = JsonContent.Create(new { clientId })
        };
        request.Headers.Add("Cookie", $"{options.Cookie.Name}={options.TicketDataFormat.Protect(ticket)}");
        request.Headers.Add("X-RateLimit-Key", $"platform-admin-session-{Guid.NewGuid():N}");
        return request;
    }
}
