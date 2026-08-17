using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.BillingGrpc;
using His.Hope.BillingService.Domain.Aggregates;
using His.Hope.BillingService.Infrastructure.Persistence;
using His.Hope.BillingService.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace His.Hope.BillingService.Integration.Tests;

public sealed class InvoiceEndpointsTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_POSTGRES_CONNECTION") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("hishopetest")
                .WithUsername("testuser")
                .WithPassword("testpass123!")
                .WithCleanUp(true)
                .Build();

            await _container.StartAsync();
            _connectionString = _container.GetConnectionString();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDb", _connectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BillingDbContext>>();
                services.RemoveAll<BillingDbContext>();
                services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddDistributedMemoryCache();

                services.AddAuthentication(BillingTestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, BillingTestAuthHandler>(BillingTestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = BillingTestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = BillingTestAuthHandler.Scheme;
                });
            });
        });

        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetInvoice_DeniesResourceOutsideAuthenticatedFacility()
    {
        var invoiceId = await SeedInvoiceAsync("facility-2");

        var response = await _client.GetAsync($"/api/v1/invoices/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetInvoice_AllowsResourceInsideAuthenticatedFacility()
    {
        var invoiceId = await SeedInvoiceAsync("facility-1");

        var response = await _client.GetAsync($"/api/v1/invoices/{invoiceId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VoidInvoice_DeniesResourceOutsideAuthenticatedFacility()
    {
        var invoiceId = await SeedInvoiceAsync("facility-2");
        var response = await _client.PutAsJsonAsync($"/api/v1/invoices/{invoiceId}/void", new { Reason = "RBAC integration" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetInvoice_DeniesResourceOutsideAuthenticatedFacility()
    {
        var invoiceId = await SeedInvoiceAsync("facility-2");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new BillingGrpcService.BillingGrpcServiceClient(channel);

        var act = async () => await client.GetInvoiceAsync(new InvoiceRequest { Id = invoiceId.ToString() });

        var error = await act.Should().ThrowAsync<RpcException>();
        error.Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetInvoice_AllowsResourceInsideAuthenticatedFacility()
    {
        var invoiceId = await SeedInvoiceAsync("facility-1");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new BillingGrpcService.BillingGrpcServiceClient(channel);

        var response = await client.GetInvoiceAsync(new InvoiceRequest { Id = invoiceId.ToString() });

        response.Id.Should().Be(invoiceId.ToString());
    }

    private async Task<Guid> SeedInvoiceAsync(string facilityId)
    {
        var invoice = Invoice.Create(
            Guid.NewGuid(), null, $"RBAC-{Guid.NewGuid():N}", DateTime.UtcNow, null, null);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        context.Invoices.Add(invoice);
        context.Entry(invoice).Property(nameof(Invoice.FacilityId)).CurrentValue = facilityId;
        await context.SaveChangesAsync();
        return invoice.Id.Value;
    }
}

public sealed class BillingTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "BillingTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim("sub", "billing-test-user"));
        identity.AddClaim(new Claim("facility_id", "facility-1"));
        identity.AddClaim(new Claim("permissions", "billing.view,billing.create,billing.pay,billing.void"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "BillingClerk"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
