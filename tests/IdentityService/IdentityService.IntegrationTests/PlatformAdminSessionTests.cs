using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Testing;
using His.Hope.SharedKernel.Authorization;
using His.Hope.SharedKernel.Protocol;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

[Collection("IdentityServiceIntegration")]
public sealed class PlatformAdminSessionTests(IdentityServiceTestFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Configured_platform_admin_can_exchange_and_list_clients_without_tenant_membership(
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
        configuration["Identity:SuperAdmin:RestrictToControlPlane"] = restrictToControlPlane.ToString();
        try
        {
            using var request = await ExchangeRequestAsync(scope.ServiceProvider, admin, "his-hope-admin");
            using var response = await fixture.AnonymousClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            using var session = fixture.CreateSessionClient();
            session.ApplySetCookieHeaders(response);
            using var clients = await session.GetWithCookiesAsync(IdentityApiRoutes.AdminClients);
            Assert.Equal(HttpStatusCode.OK, clients.StatusCode);
            Assert.Empty((await users.GetClaimsAsync(admin))
                .Where(claim => claim.Type == HisHopeProtocolConstants.Claims.TenantMembership));
        }
        finally
        {
            configuration["Identity:SuperAdmin:RestrictToControlPlane"] = previousRestriction;
            Assert.True((await users.AddClaimsAsync(admin, memberships)).Succeeded);
        }
    }

    [Fact]
    public async Task Tenant_admin_cannot_use_platform_admin_client_even_with_hq_membership()
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
                new Claim(HisHopeProtocolConstants.Claims.TenantMembership, "group-hq"))).Succeeded);
            using var request = await ExchangeRequestAsync(scope.ServiceProvider, user, "his-hope-admin");
            var identityCookie = request.Headers.GetValues("Cookie").Single();
            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", $"{identityCookie}; hishop_sid={oldSessionId}");
            using var response = await fixture.AnonymousClient.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
            Assert.True(await redis.KeyExistsAsync(oldSessionKey));

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
