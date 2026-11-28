using System.Net;
using System.Security.Cryptography;
using System.Text;
using His.Hope.IdentityService.Application.Interfaces;
using His.Hope.IdentityService.Domain.Entities;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class SecuritySignalDispatcherTests
{
    [Fact]
    public async Task Dispatcher_delivers_pending_signal_and_marks_it_dispatched()
    {
        await using var harness = await CreateHarnessAsync(new RecordingHandler(HttpStatusCode.Accepted));
        await harness.SeedAsync();
        await harness.Dispatcher.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(x => x.DispatchedAt is not null);
        await harness.StopAsync();

        Assert.NotNull(item.DispatchedAt);
        Assert.Null(item.LastError);
        Assert.Null(item.LeaseId);
        Assert.Equal(1, harness.Handler.Calls);
        Assert.Contains("secevent+jwt", harness.Handler.LastContentType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatcher_retries_failed_delivery_and_clears_lease()
    {
        await using var harness = await CreateHarnessAsync(new RecordingHandler(HttpStatusCode.BadGateway));
        await harness.SeedAsync();
        await harness.Dispatcher.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(x => x.LastError is not null);
        await harness.StopAsync();

        Assert.Null(item.DispatchedAt);
        Assert.Equal(1, item.Attempts);
        Assert.NotNull(item.LastError);
        Assert.True(item.AvailableAt > DateTime.UtcNow);
        Assert.Null(item.LeaseId);
        Assert.Null(item.LeaseUntil);
    }

    [Fact]
    public async Task Dispatcher_ignores_non_https_subscriptions()
    {
        await using var harness = await CreateHarnessAsync(new RecordingHandler(HttpStatusCode.Accepted), "http://insecure.example.test/events");
        await harness.SeedAsync();
        await harness.Dispatcher.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await harness.StopAsync();

        Assert.Equal(0, harness.Handler.Calls);
        using var scope = harness.Provider.CreateScope();
        var item = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().SecuritySignalOutbox.SingleAsync();
        Assert.Null(item.DispatchedAt);
        Assert.Null(item.LeaseId);
    }

    [Fact]
    public async Task Dispatcher_does_nothing_when_feature_is_disabled()
    {
        await using var harness = await CreateHarnessAsync(new RecordingHandler(HttpStatusCode.Accepted), enabled: false);
        await harness.SeedAsync();
        await harness.Dispatcher.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await harness.StopAsync();

        Assert.Equal(0, harness.Handler.Calls);
        using var scope = harness.Provider.CreateScope();
        var item = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().SecuritySignalOutbox.SingleAsync();
        Assert.Null(item.DispatchedAt);
        Assert.Null(item.LeaseId);
    }

    [Fact]
    public async Task Dispatcher_delivers_to_each_https_subscription_and_maps_unknown_event_type()
    {
        await using var harness = await CreateHarnessAsync(
            new RecordingHandler(HttpStatusCode.Accepted),
            urls: ["https://consumer-one.example.test/events", "https://consumer-two.example.test/events"]);
        await harness.SeedAsync(eventType: "custom event");
        await harness.Dispatcher.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(x => x.DispatchedAt is not null);
        await harness.StopAsync();

        Assert.NotNull(item.DispatchedAt);
        Assert.Equal(2, harness.Handler.Calls);
        Assert.NotNull(harness.Handler.LastBody);
        var payload = DecodeJwtPayload(harness.Handler.LastBody!);
        Assert.Contains("his-hope.com/secevent/event-type/custom%20event", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispatcher_retries_when_outbox_payload_is_not_valid_json()
    {
        await using var harness = await CreateHarnessAsync(new RecordingHandler(HttpStatusCode.Accepted));
        await harness.SeedAsync(payloadJson: "not-json");
        await harness.Dispatcher.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(x => x.LastError is not null);
        await harness.StopAsync();

        Assert.Null(item.DispatchedAt);
        Assert.Equal(1, item.Attempts);
        Assert.Contains("JSON", item.LastError!, StringComparison.OrdinalIgnoreCase);
        Assert.Null(item.LeaseId);
        Assert.Null(item.LeaseUntil);
    }

    [Theory]
    [InlineData("credential-change", "credential-change")]
    [InlineData("credential_changed", "credential-change")]
    [InlineData("password-change", "credential-change")]
    [InlineData("mfa-device-change", "mfa-device-change")]
    [InlineData("mfa_device_changed", "mfa-device-change")]
    [InlineData("session-revoked", "session-revoked")]
    [InlineData("session_revoked", "session-revoked")]
    public async Task Dispatcher_maps_standard_security_event_aliases(string eventType, string expectedEventType)
    {
        await using var harness = await CreateHarnessAsync(new RecordingHandler(HttpStatusCode.Accepted));
        await harness.SeedAsync(eventType: eventType);
        await harness.Dispatcher.StartAsync(CancellationToken.None);

        var item = await harness.WaitForAsync(x => x.DispatchedAt is not null);
        await harness.StopAsync();

        Assert.NotNull(item.DispatchedAt);
        var payload = DecodeJwtPayload(harness.Handler.LastBody!);
        Assert.Contains($"https://schemas.openid.net/secevent/caep/event-type/{expectedEventType}", payload, StringComparison.Ordinal);
    }

    private static async Task<Harness> CreateHarnessAsync(
        RecordingHandler handler,
        string url = "https://consumer.example.test/events",
        bool enabled = true,
        IReadOnlyList<string>? urls = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        var keyProvider = new Mock<IVaultKeyProvider>();
        keyProvider.Setup(x => x.GetSigningKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RsaSecurityKey(RSA.Create(2048)));
        services.AddSingleton(keyProvider.Object);
        var provider = services.BuildServiceProvider();
        var configuredUrls = urls ?? [url];
        var configurationValues = new Dictionary<string, string?>
        {
            ["SSF_ENABLED"] = enabled.ToString(),
            ["OpenIddict:Issuer"] = "https://identity.example.test"
        };
        for (var index = 0; index < configuredUrls.Count; index++)
        {
            configurationValues[$"SecuritySignals:Subscriptions:{index}:Url"] = configuredUrls[index];
            configurationValues[$"SecuritySignals:Subscriptions:{index}:Audience"] = $"consumer-api-{index}";
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(x => x.CreateClient("security-signals"))
            .Returns(new HttpClient(handler) { BaseAddress = new Uri("https://identity.example.test") });
        var dispatcher = new SecuritySignalDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(), clientFactory.Object, keyProvider.Object,
            configuration, NullLogger<SecuritySignalDispatcher>.Instance);
        return new Harness(provider, connection, dispatcher, handler);
    }

    private static string DecodeJwtPayload(string token)
    {
        var segment = token.Split('.')[1];
        segment = segment.Replace('-', '+').Replace('_', '/');
        segment = segment.PadRight(segment.Length + ((4 - segment.Length % 4) % 4), '=');
        return Encoding.UTF8.GetString(Convert.FromBase64String(segment));
    }

    private sealed class Harness(
        ServiceProvider provider,
        SqliteConnection connection,
        SecuritySignalDispatcher dispatcher,
        RecordingHandler handler) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;
        public SecuritySignalDispatcher Dispatcher { get; } = dispatcher;
        public RecordingHandler Handler { get; } = handler;

        public async Task SeedAsync(string eventType = "logout", string payloadJson = "{\"sid\":\"session-1\"}")
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            db.SecuritySignalOutbox.Add(new SecuritySignalOutbox
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Subject = "user-1",
                PayloadJson = payloadJson,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                AvailableAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }

        public async Task<SecuritySignalOutbox> WaitForAsync(Func<SecuritySignalOutbox, bool> predicate)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(25);
                using var scope = Provider.CreateScope();
                var item = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
                    .SecuritySignalOutbox.AsNoTracking().SingleAsync();
                if (predicate(item)) return item;
            }

            throw new TimeoutException("Security signal dispatcher did not process the outbox item.");
        }

        public async Task StopAsync()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Dispatcher.StopAsync(timeout.Token);
        }

        public async ValueTask DisposeAsync()
        {
            Dispatcher.Dispose();
            await Provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? LastContentType { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastContentType = request.Content?.Headers.ContentType?.MediaType;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }
}
