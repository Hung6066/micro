using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.PharmacyGrpc;
using His.Hope.PharmacyService.Domain.Aggregates;
using His.Hope.PharmacyService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace His.Hope.PharmacyService.Integration.Tests;

public sealed class PrescriptionEndpointsTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = string.Empty;

    static PrescriptionEndpointsTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("PHARMACY_TEST_POSTGRES_CONNECTION") ?? string.Empty;
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
            builder.UseSetting("ConnectionStrings:PharmacyDb", _connectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PharmacyDbContext>>();
                services.RemoveAll<PharmacyDbContext>();
                services.AddDbContext<PharmacyDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddAuthentication(PharmacyTestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, PharmacyTestAuthHandler>(PharmacyTestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = PharmacyTestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = PharmacyTestAuthHandler.Scheme;
                });
            });
        });

        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
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
    public async Task GetPrescription_DeniesResourceOutsideAuthenticatedFacility()
    {
        var prescriptionId = await SeedPrescriptionAsync("facility-2");

        var response = await _client.GetAsync($"/api/v1/prescriptions/{prescriptionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPrescription_AllowsResourceInsideAuthenticatedFacility()
    {
        var prescriptionId = await SeedPrescriptionAsync("facility-1");

        var response = await _client.GetAsync($"/api/v1/prescriptions/{prescriptionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FillPrescription_DeniesResourceOutsideAuthenticatedFacility()
    {
        var prescriptionId = await SeedPrescriptionAsync("facility-2");

        var response = await _client.PutAsync($"/api/v1/prescriptions/{prescriptionId}/fill", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetPrescription_DeniesResourceOutsideAuthenticatedFacility()
    {
        var prescriptionId = await SeedPrescriptionAsync("facility-2");
        var client = CreateGrpcClient();

        var act = () => client.GetPrescriptionAsync(new PrescriptionRequest { Id = prescriptionId.ToString() }).ResponseAsync;

        var exception = await Assert.ThrowsAsync<RpcException>(act);
        exception.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetPrescription_AllowsResourceInsideAuthenticatedFacility()
    {
        var prescriptionId = await SeedPrescriptionAsync("facility-1");
        var client = CreateGrpcClient();

        var response = await client.GetPrescriptionAsync(
            new PrescriptionRequest { Id = prescriptionId.ToString() }).ResponseAsync;

        response.Id.Should().Be(prescriptionId.ToString());
    }

    private PharmacyGrpcService.PharmacyGrpcServiceClient CreateGrpcClient() =>
        new(GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        }));

    private async Task<Guid> SeedPrescriptionAsync(string facilityId)
    {
        var prescription = Prescription.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "Test medication", "10 mg", "Tablet",
            "Take once daily", "oral", 30, 1, "RBAC integration", DateTime.UtcNow.AddDays(30));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        context.Prescriptions.Add(prescription);
        context.Entry(prescription).Property(nameof(Prescription.FacilityId)).CurrentValue = facilityId;
        await context.SaveChangesAsync();
        return prescription.Id.Value;
    }
}

public sealed class PharmacyTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "PharmacyTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim("sub", "pharmacy-test-user"));
        identity.AddClaim(new Claim("facility_id", "facility-1"));
        identity.AddClaim(new Claim("permissions", "pharmacy.view,pharmacy.create,pharmacy.update,pharmacy.dispense"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Pharmacist"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
