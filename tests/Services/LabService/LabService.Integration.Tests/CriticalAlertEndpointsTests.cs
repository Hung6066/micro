using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using His.Hope.LabService.Application.DTOs;
using His.Hope.LabService.Domain.Aggregates;
using His.Hope.LabService.Domain.Entities;
using His.Hope.LabService.Domain.ValueObjects;
using His.Hope.LabService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace His.Hope.LabService.Integration.Tests;

public class CriticalAlertEndpointsTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("hishopetest")
            .WithUsername("testuser")
            .WithPassword("testpass123!")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LabDbContext>>();
                services.RemoveAll<LabDbContext>();
                services.AddDbContext<LabDbContext>(options => options.UseNpgsql(_container.GetConnectionString()));
                services.AddDistributedMemoryCache();

                services.AddAuthentication(TestAuthHandler.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });

                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    options.DefaultChallengeScheme = TestAuthHandler.Scheme;
                });
            });
        });

        _client = _factory.CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task CreateRule_And_ListRules_ReturnCreatedRule()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/critical-alert-rules", new
        {
            testCode = "CBC",
            testName = "Complete Blood Count",
            unit = "x10^9/L",
            lowCriticalValue = (decimal?)null,
            highCriticalValue = 10.0m
        });

        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CriticalAlertRuleDto>();
        created.Should().NotBeNull();
        created!.TestCode.Should().Be("CBC");

        var rules = await _client.GetFromJsonAsync<List<CriticalAlertRuleDto>>("/api/v1/critical-alert-rules");
        rules.Should().ContainSingle(rule => rule.TestCode == "CBC" && rule.HighCriticalValue == 10.0m);
    }

    [Fact]
    public async Task CriticalResultSave_CreatesSingleOpenAlert_AndInboxListsIt()
    {
        var (orderId, testId) = await SeedOrderWithCollectedTestAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/lab-orders/{orderId}/result", new
        {
            testId,
            value = "18.5",
            abnormalFlagCode = "CRITICAL_HIGH",
            notes = "Critical CBC"
        });

        response.EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        var alerts = await context.CriticalAlerts.Include(x => x.AuditEntries).ToListAsync();

        alerts.Should().ContainSingle();
        alerts[0].Status.Should().Be(CriticalAlertStatus.Open);
        alerts[0].AuditEntries.Should().ContainSingle(entry => entry.Action == "Created");

        var inbox = await _client.GetFromJsonAsync<List<CriticalAlertDto>>("/api/v1/critical-alerts");
        inbox.Should().ContainSingle(alert => alert.Status == CriticalAlertStatus.Open);
    }

    [Fact]
    public async Task CriticalResultSave_EmitsCreatedRealtimeAlert()
    {
        var (orderId, testId) = await SeedOrderWithCollectedTestAsync();
        var received = new TaskCompletionSource<CriticalAlertDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = await CreateHubConnectionAsync();
        connection.On<CriticalAlertDto>("criticalAlertCreated", alert => received.TrySetResult(alert));
        await connection.StartAsync();

        var response = await _client.PutAsJsonAsync($"/api/v1/lab-orders/{orderId}/result", new
        {
            testId,
            value = "18.5",
            abnormalFlagCode = "CRITICAL_HIGH",
            notes = "Critical CBC"
        });

        response.EnsureSuccessStatusCode();

        var alert = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        alert.Status.Should().Be(CriticalAlertStatus.Open);
        alert.AuditEntries.Should().ContainSingle(entry => entry.Action == "Created");
    }

    [Fact]
    public async Task AcknowledgeAndResolve_UpdateAlertAndPreserveAuditTrail()
    {
        var orderId = Guid.NewGuid();
        var testId = Guid.NewGuid();
        var alertId = await SeedOpenAlertAsync(orderId, testId);

        var acknowledgeResponse = await _client.PostAsync($"/api/v1/critical-alerts/{alertId}/acknowledge", null);
        acknowledgeResponse.EnsureSuccessStatusCode();

        var resolveResponse = await _client.PostAsync($"/api/v1/critical-alerts/{alertId}/resolve", null);
        resolveResponse.EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        var alert = await context.CriticalAlerts.Include(x => x.AuditEntries).SingleAsync(x => x.Id == alertId);

        alert.Status.Should().Be(CriticalAlertStatus.Resolved);
        alert.AcknowledgedAt.Should().NotBeNull();
        alert.ResolvedAt.Should().NotBeNull();
        alert.AuditEntries.Should().Contain(entry => entry.Action == "Acknowledged");
        alert.AuditEntries.Should().Contain(entry => entry.Action == "Resolved");
    }

    [Fact]
    public void RealtimePublisher_Type_ShouldExposeStateMethods()
    {
        var publisherType = typeof(Program).Assembly.GetType("His.Hope.LabService.Api.Services.CriticalAlertRealtimePublisher");

        publisherType.Should().NotBeNull();
        publisherType!.GetMethod("PublishCreatedAsync").Should().NotBeNull();
        publisherType.GetMethod("PublishUpdatedAsync").Should().NotBeNull();
        publisherType.GetMethod("PublishAcknowledgedAsync").Should().NotBeNull();
        publisherType.GetMethod("PublishResolvedAsync").Should().NotBeNull();
    }

    private async Task<(Guid OrderId, Guid TestId)> SeedOrderWithCollectedTestAsync()
    {
        var order = LabOrder.Create(Guid.NewGuid(), Guid.NewGuid(), null, LabOrderPriority.Routine, null);
        var test = LabTest.Create(order.Id, "CBC", "Complete Blood Count", "x10^9/L");
        order.AddTest(test);
        test.MarkCollected();
        test.MarkInProgress();

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        context.LabOrders.Add(order);
        await context.SaveChangesAsync();

        return (order.Id.Value, test.Id.Value);
    }

    private async Task<Guid> SeedOpenAlertAsync(Guid orderId, Guid testId)
    {
        var alert = CriticalAlert.Create(
            orderId,
            testId,
            Guid.NewGuid(),
            null,
            CriticalAlertTriggerType.CriticalFlag,
            "Critical flag CRITICAL_HIGH",
            "18.5",
            "x10^9/L",
            null,
            "user-1",
            "Dr. Jones");

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LabDbContext>();
        context.CriticalAlerts.Add(alert);
        await context.SaveChangesAsync();

        return alert.Id;
    }

    private Task<HubConnection> CreateHubConnectionAsync()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress!, "/hubs/lab-critical-alerts"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .WithAutomaticReconnect()
            .Build();

        return Task.FromResult(connection);
    }
}

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public new const string Scheme = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim("sub", "user-1"));
        identity.AddClaim(new Claim("fullName", "Dr. Jones"));
        identity.AddClaim(new Claim("permissions", "lab.view,lab.manage,lab.result"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "LabTechnician"));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
