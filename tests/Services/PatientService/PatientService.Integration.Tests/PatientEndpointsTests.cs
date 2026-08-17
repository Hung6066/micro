using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.PatientGrpc;
using His.Hope.PatientService.Domain.Aggregates;
using His.Hope.PatientService.Domain.ValueObjects;
using His.Hope.PatientService.Infrastructure.Persistence;
using His.Hope.SharedKernel.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace His.Hope.PatientService.Integration.Tests;

public sealed class PatientEndpointsTests : IAsyncLifetime
{
    static PatientEndpointsTests()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("PATIENT_TEST_POSTGRES_CONNECTION") ?? string.Empty;
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
            builder.UseSetting("ConnectionStrings:PatientDb", _connectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PatientDbContext>>();
                services.RemoveAll<PatientDbContext>();
                services.AddDbContext<PatientDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddDistributedMemoryCache();
                services.AddAuthentication(PatientTestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, PatientTestAuthHandler>(PatientTestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = PatientTestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = PatientTestAuthHandler.Scheme;
                });
            });
        });

        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PatientDbContext>();
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
    public async Task GetPatient_DeniesResourceOutsideAuthenticatedFacility()
    {
        var patientId = await SeedPatientAsync("facility-2");

        var response = await _client.GetAsync($"/api/v1/patients/{patientId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPatient_AllowsResourceInsideAuthenticatedFacility()
    {
        var patientId = await SeedPatientAsync("facility-1");

        var response = await _client.GetAsync($"/api/v1/patients/{patientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivatePatient_DeniesResourceOutsideAuthenticatedFacility()
    {
        var patientId = await SeedPatientAsync("facility-2");
        var response = await _client.PatchAsync($"/api/v1/patients/{patientId}/deactivate", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetPatient_DeniesResourceOutsideAuthenticatedFacility()
    {
        var patientId = await SeedPatientAsync("facility-2");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new PatientGrpcService.PatientGrpcServiceClient(channel);

        var act = async () => await client.GetPatientAsync(new PatientRequest { Id = patientId.ToString() });

        var error = await act.Should().ThrowAsync<RpcException>();
        error.Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetPatient_AllowsResourceInsideAuthenticatedFacility()
    {
        var patientId = await SeedPatientAsync("facility-1");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new PatientGrpcService.PatientGrpcServiceClient(channel);

        var response = await client.GetPatientAsync(new PatientRequest { Id = patientId.ToString() });

        response.Id.Should().Be(patientId.ToString());
    }

    private async Task<Guid> SeedPatientAsync(string facilityId)
    {
        var patient = Patient.Register(
            PersonName.Create("RBAC", $"Patient{Guid.NewGuid():N}"),
            new DateTime(1990, 5, 15),
            Gender.Male,
            ContactInfo.Create($"+1{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}", $"rbac-{Guid.NewGuid():N}@example.test"),
            Address.Create("1 Test Street", "District", "City", "Province", "10000", "VN"));

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PatientDbContext>();
        context.Patients.Add(patient);
        context.Entry(patient).Property(nameof(Patient.FacilityId)).CurrentValue = facilityId;
        await context.SaveChangesAsync();
        return patient.Id.Value;
    }
}

public sealed class PatientTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "PatientTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim("sub", "patient-test-user"));
        identity.AddClaim(new Claim("facility_id", "facility-1"));
        identity.AddClaim(new Claim("permissions", "patients.view,patients.create,patients.update,patients.delete"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Clinician"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
