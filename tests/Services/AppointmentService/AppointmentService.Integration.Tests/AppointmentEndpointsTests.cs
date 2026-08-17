using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Grpc.Core;
using Grpc.Net.Client;
using His.Hope.AppointmentGrpc;
using His.Hope.AppointmentService.Domain.Aggregates;
using His.Hope.AppointmentService.Domain.ValueObjects;
using His.Hope.AppointmentService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace His.Hope.AppointmentService.Integration.Tests;

public sealed class AppointmentEndpointsTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = Environment.GetEnvironmentVariable("APPOINTMENT_TEST_POSTGRES_CONNECTION") ?? string.Empty;
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
            builder.UseSetting("ConnectionStrings:AppointmentDb", _connectionString);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppointmentDbContext>>();
                services.RemoveAll<AppointmentDbContext>();
                services.AddDbContext<AppointmentDbContext>(options => options.UseNpgsql(_connectionString));
                services.AddAuthentication(AppointmentTestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, AppointmentTestAuthHandler>(AppointmentTestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = AppointmentTestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = AppointmentTestAuthHandler.Scheme;
                });
            });
        });

        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
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
    public async Task GetAppointment_DeniesResourceOutsideAuthenticatedFacility()
    {
        var appointmentId = await SeedAppointmentAsync("facility-2");

        var response = await _client.GetAsync($"/api/v1/appointments/{appointmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAppointment_AllowsResourceInsideAuthenticatedFacility()
    {
        var appointmentId = await SeedAppointmentAsync("facility-1");

        var response = await _client.GetAsync($"/api/v1/appointments/{appointmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CheckinAppointment_DeniesResourceOutsideAuthenticatedFacility()
    {
        var appointmentId = await SeedAppointmentAsync("facility-2");
        var response = await _client.PutAsync($"/api/v1/appointments/{appointmentId}/checkin", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetAppointment_DeniesResourceOutsideAuthenticatedFacility()
    {
        var appointmentId = await SeedAppointmentAsync("facility-2");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new AppointmentGrpcService.AppointmentGrpcServiceClient(channel);

        var act = async () => await client.GetAppointmentAsync(new AppointmentRequest { Id = appointmentId.ToString() });

        var error = await act.Should().ThrowAsync<RpcException>();
        error.Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [Fact]
    public async Task GrpcGetAppointment_AllowsResourceInsideAuthenticatedFacility()
    {
        var appointmentId = await SeedAppointmentAsync("facility-1");
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = _factory.Server.CreateHandler()
        });
        var client = new AppointmentGrpcService.AppointmentGrpcServiceClient(channel);

        var response = await client.GetAppointmentAsync(new AppointmentRequest { Id = appointmentId.ToString() });

        response.Id.Should().Be(appointmentId.ToString());
    }

    private async Task<Guid> SeedAppointmentAsync(string facilityId)
    {
        var appointment = Appointment.Schedule(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.Date.AddDays(2),
            new TimeSpan(9, 0, 0), 30, AppointmentType.Checkup,
            "RBAC integration", "Clinic");

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
        context.Appointments.Add(appointment);
        context.Entry(appointment).Property(nameof(Appointment.FacilityId)).CurrentValue = facilityId;
        await context.SaveChangesAsync();
        return appointment.Id.Value;
    }
}

public sealed class AppointmentTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "AppointmentTest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim("sub", "appointment-test-user"));
        identity.AddClaim(new Claim("facility_id", "facility-1"));
        identity.AddClaim(new Claim("permissions", "appointments.view,appointments.create,appointments.update,appointments.cancel,appointments.check-in"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Scheduler"));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
    }
}
