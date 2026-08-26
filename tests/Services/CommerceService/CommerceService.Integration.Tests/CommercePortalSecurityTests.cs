using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using His.Hope.Authorization;
using His.Hope.CommerceService.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CommerceService.Integration.Tests;

public sealed class CommercePortalSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CommercePortalSecurityTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                services.PostConfigureAll<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });
            });
        });

    [Fact]
    public async Task Buyer_catalog_rejects_operator_portal_class()
    {
        var client = CreateClient(PortalClassConstants.Operator, "customer-factory-x", ["commerce.read", "commerce.catalog.view"]);
        var response = await client.GetAsync("/api/v1/commerce/products");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("forbidden", problem.GetProperty("errorCode").GetString());
        Assert.Equal(403, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Buyer_catalog_accepts_end_user_portal_class()
    {
        var client = CreateClient(PortalClassConstants.EndUser, "customer-factory-x", ["commerce.read", "commerce.catalog.view"]);
        var response = await client.GetAsync("/api/v1/commerce/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_status_update_rejects_end_user_portal_class()
    {
        var client = CreateClient(PortalClassConstants.EndUser, "customer-factory-x", ["commerce.write", "commerce.orders.update"]);
        var payload = new StringContent("{\"status\":\"confirmed\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PatchAsync("/api/v1/commerce/orders/00000000-0000-0000-0000-000000000001/status", payload);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Buyer_cart_empty_uses_shared_problem_details_contract()
    {
        var client = CreateClient(
            PortalClassConstants.EndUser,
            "customer-factory-x",
            ["commerce.write", "commerce.orders.create"]);

        var response = await client.PostAsync("/api/v1/commerce/orders", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("cart_empty", problem.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Missing_order_uses_shared_problem_details_contract()
    {
        var client = CreateClient(
            PortalClassConstants.EndUser,
            "customer-factory-x",
            ["commerce.read", "commerce.orders.view"]);

        var response = await client.GetAsync("/api/v1/commerce/orders/00000000-0000-0000-0000-000000000099");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", problem.GetProperty("errorCode").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Cross_tenant_write_denial_uses_shared_problem_details_contract()
    {
        var client = CreateClient(
            PortalClassConstants.Operator,
            "source-tenant",
            ["commerce.read", "commerce.orders.view", "commerce.orders.update"]);

        var response = await client.PatchAsJsonAsync(
            "/api/v1/commerce/orders/00000000-0000-0000-0000-000000000001/status?tenantKey=target-tenant",
            new { status = "confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("support_elevation_required", problem.GetProperty("errorCode").GetString());
        Assert.Equal(403, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }

    private HttpClient CreateClient(string portalClass, string tenantId, IEnumerable<string> permissions)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Portal-Class", portalClass);
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenantId);
        client.DefaultRequestHeaders.Add("X-Test-Permissions", string.Join(',', permissions));
        client.DefaultRequestHeaders.Add("X-Test-Scopes", "commerce.read commerce.write");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        return client;
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Portal-Class", out var portalClass))
                return Task.FromResult(AuthenticateResult.Fail("missing test auth"));

            var tenant = Request.Headers["X-Test-Tenant"].FirstOrDefault() ?? "customer-factory-x";
            var permissions = Request.Headers["X-Test-Permissions"].FirstOrDefault() ?? string.Empty;
            var scopes = Request.Headers["X-Test-Scopes"].FirstOrDefault() ?? string.Empty;

            var claims = new List<Claim>
            {
                new("sub", Guid.NewGuid().ToString()),
                new(PortalClassConstants.Claim, portalClass.ToString()),
                new("tenant_id", tenant),
                new("client_id", "test-client"),
                new("scope", scopes),
                new("permissions", permissions),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
