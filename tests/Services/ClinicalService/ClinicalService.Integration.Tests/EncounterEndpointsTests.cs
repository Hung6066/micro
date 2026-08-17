using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.ClinicalGrpc;
using His.Hope.ClinicalService.Domain.Aggregates;
using His.Hope.ClinicalService.Domain.ValueObjects;
using His.Hope.ClinicalService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace His.Hope.ClinicalService.Integration.Tests;

public sealed class EncounterEndpointsTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = string.Empty;

    static EncounterEndpointsTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("CLINICAL_TEST_POSTGRES_CONNECTION") ?? string.Empty;
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
            builder.UseSetting("ConnectionStrings:ClinicalDb", _connectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ClinicalDbContext>>();
                services.RemoveAll<ClinicalDbContext>();
                services.AddDbContext<ClinicalDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddAuthentication(ClinicalTestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, ClinicalTestAuthHandler>(ClinicalTestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = ClinicalTestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = ClinicalTestAuthHandler.Scheme;
                });
            });
        });

        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
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
    public async Task GetEncounter_DeniesResourceOutsideAuthenticatedFacility()
    {
        var encounterId = await SeedEncounterAsync("facility-2");

        var response = await _client.GetAsync($"/api/v1/encounters/{encounterId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetEncounter_AllowsResourceInsideAuthenticatedFacility()
    {
        var encounterId = await SeedEncounterAsync("facility-1");

        var response = await _client.GetAsync($"/api/v1/encounters/{encounterId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompleteEncounter_DeniesResourceOutsideAuthenticatedFacility()
    {
        var encounterId = await SeedEncounterAsync("facility-2");
        var response = await _client.PutAsync($"/api/v1/encounters/{encounterId}/complete", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetEncounter_DeniesResourceOutsideAuthenticatedFacility()
    {
        var encounterId = await SeedEncounterAsync("facility-2");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new ClinicalGrpcService.ClinicalGrpcServiceClient(channel);

        var act = async () => await client.GetEncounterAsync(new EncounterRequest { Id = encounterId.ToString() });

        var error = await act.Should().ThrowAsync<RpcException>();
        error.Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetEncounter_AllowsResourceInsideAuthenticatedFacility()
    {
        var encounterId = await SeedEncounterAsync("facility-1");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new ClinicalGrpcService.ClinicalGrpcServiceClient(channel);

        var response = await client.GetEncounterAsync(new EncounterRequest { Id = encounterId.ToString() });

        response.Id.Should().Be(encounterId.ToString());
    }

    private async Task<Guid> SeedEncounterAsync(string facilityId)
    {
        var encounter = Encounter.Start(Guid.NewGuid(), Guid.NewGuid(), EncounterType.Outpatient);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
        context.Encounters.Add(encounter);
        context.Entry(encounter).Property(nameof(Encounter.FacilityId)).CurrentValue = facilityId;
        await context.SaveChangesAsync();
        return encounter.Id.Value;
    }
}

public sealed class ClinicalTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "ClinicalTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim("sub", "clinical-test-user"));
        identity.AddClaim(new Claim("facility_id", "facility-1"));
        identity.AddClaim(new Claim("permissions", "clinical.view,clinical.create,clinical.update,clinical.sign"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Clinician"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
