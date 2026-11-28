using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using His.Hope.Contracts.Identity;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

/// <summary>
/// A client-credentials workload token must carry the workload claims that
/// resource policies rely on. OpenIddict drops claims without destinations, so
/// a token without them is authenticated but fails every workload policy.
/// </summary>
[Collection("IdentityServiceIntegration")]
public sealed class OpenShipWorkloadTokenTests
{
    private readonly IdentityServiceTestFixture _fixture;

    public OpenShipWorkloadTokenTests(IdentityServiceTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Client_credentials_token_satisfies_the_openship_managed_client_policy()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var clientId = $"openship-provisioner-{suffix}";
        var clientSecret = $"secret-{Guid.NewGuid():N}{Guid.NewGuid():N}";

        await using (var seed = _fixture.Services.CreateAsyncScope())
        {
            var configuration = seed.ServiceProvider.GetRequiredService<IConfiguration>();
            var audience = configuration["Jwt:Audience"];
            Assert.False(string.IsNullOrWhiteSpace(audience));

            var db = seed.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var scope = new IamScope { Key = $"tenant-{suffix}", DisplayName = "OpenShip workload tenant", Kind = "tenant" };
            db.IamScopes.Add(scope);
            db.IamWorkloadRoles.Add(new IamWorkloadRole
            {
                Key = clientId,
                DisplayName = "OpenShip runtime provisioner",
                ScopeId = scope.Id,
                Audience = audience!,
                TrustPolicyJson = JsonSerializer.Serialize(new { principals = new[] { clientId } }),
                PermissionsJson = "[]",
                MaxSessionSeconds = 900
            });
            await db.SaveChangesAsync();

            var scopes = seed.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
            if (await scopes.FindByNameAsync(IdentityApiRoutes.OpenShipClientReconcileScope) is null)
            {
                var scopeDescriptor = new OpenIddictScopeDescriptor { Name = IdentityApiRoutes.OpenShipClientReconcileScope };
                scopeDescriptor.Resources.Add(audience!);
                await scopes.CreateAsync(scopeDescriptor);
            }

            var applications = seed.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                DisplayName = "OpenShip runtime provisioner"
            };
            descriptor.Permissions.UnionWith(new[]
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + IdentityApiRoutes.OpenShipClientReconcileScope
            });
            await applications.CreateAsync(descriptor);
        }

        var tokenResponse = await _fixture.AnonymousClient.PostAsync(IdentityApiRoutes.OidcToken, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = OpenIddictConstants.GrantTypes.ClientCredentials,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = IdentityApiRoutes.OpenShipClientReconcileScope
        }));
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.IsSuccessStatusCode, tokenBody);
        var accessToken = JsonDocument.Parse(tokenBody).RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // An external key owned by some other client: the policy must admit
        // the workload, and only the endpoint's ownership check may refuse it.
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{IdentityApiRoutes.OpenShipProvisioningClients}/os-000000000000-{new string('0', 32)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await _fixture.AnonymousClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("external_key_forbidden", JsonDocument.Parse(body).RootElement.GetProperty("errorCode").GetString());
    }
}
