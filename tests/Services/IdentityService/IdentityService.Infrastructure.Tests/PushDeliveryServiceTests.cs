using FluentAssertions;
using His.Hope.IdentityService.Api.Configuration;
using His.Hope.IdentityService.Api.Services;
using His.Hope.IdentityService.Infrastructure.Persistence;
using His.Hope.IdentityService.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using His.Hope.ServiceDefaults;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace His.Hope.IdentityService.Infrastructure.Tests;

public sealed class PushDeliveryServiceTests
{
    [Fact]
    public async Task Enqueue_persists_in_app_and_outbox_notifications()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var id = await service.EnqueueAsync("user-1", "Hello", "Body", "{\"kind\":\"test\"}");

        Assert.NotEqual(Guid.Empty, id);
        Assert.Single(db.PushNotificationOutbox);
        Assert.Single(db.InAppNotifications);
        Assert.Equal(id, db.PushNotificationOutbox.Single().Id);
    }

    [Fact]
    public async Task Enqueue_rejects_invalid_payloads_and_delivery_without_devices_is_retryable()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.EnqueueAsync("", "Title", "Body"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.EnqueueAsync("user", "", "Body"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.EnqueueAsync("user", "Title", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.EnqueueAsync("user", "Title", "Body", new string('x', 8001)));

        Assert.False(await service.DeliverAsync("user-without-device", "Title", "Body"));
        Assert.Empty(db.PushDeliveryAttempts);
    }

    [Fact]
    public async Task Deliver_android_sends_firebase_and_records_success()
    {
        await using var db = CreateDb();
        using var rsa = RSA.Create(2048);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"oauth-token\"}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK));
        var protector = DataProtectionProvider.Create("IdentityService.Tests.PushDelivery").CreateProtector("HisHope.Mobile.PushToken.v1");
        db.MobileDeviceRegistrations.Add(new MobileDeviceRegistration
        {
            UserId = "user-1",
            Platform = "android",
            TokenCiphertext = protector.Protect("device-token")
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, handler, $"{{\"project_id\":\"demo\",\"client_email\":\"push@example.test\",\"private_key\":{JsonString(rsa.ExportRSAPrivateKeyPem())}}}");

        var delivered = await service.DeliverAsync("user-1", "Title", "Body", Guid.NewGuid());

        delivered.Should().BeTrue();
        db.PushDeliveryAttempts.Single().Status.Should().Be("sent");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deliver_failed_provider_response_records_failed_attempt()
    {
        await using var db = CreateDb();
        var protector = DataProtectionProvider.Create("IdentityService.Tests.PushDelivery").CreateProtector("HisHope.Mobile.PushToken.v1");
        db.MobileDeviceRegistrations.Add(new MobileDeviceRegistration
        {
            UserId = "user-1",
            Platform = "android",
            TokenCiphertext = protector.Protect("device-token")
        });
        await db.SaveChangesAsync();
        using var rsa = RSA.Create(2048);
        var handler = new RecordingHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("bad token") });
        var service = CreateService(db, handler, $"{{\"project_id\":\"demo\",\"client_email\":\"push@example.test\",\"private_key\":{JsonString(rsa.ExportRSAPrivateKeyPem())}}}");

        var delivered = await service.DeliverAsync("user-1", "Title", "Body");

        delivered.Should().BeFalse();
        db.PushDeliveryAttempts.Single().Status.Should().Be("failed");
    }

    [Fact]
    public async Task Deliver_unprotectable_device_is_revoked_without_provider_call()
    {
        await using var db = CreateDb();
        db.MobileDeviceRegistrations.Add(new MobileDeviceRegistration
        {
            UserId = "user-1", Platform = "android", TokenCiphertext = "not-protected"
        });
        await db.SaveChangesAsync();
        var handler = new RecordingHandler();
        var service = CreateService(db, handler);

        var delivered = await service.DeliverAsync("user-1", "Title", "Body");

        delivered.Should().BeFalse();
        db.MobileDeviceRegistrations.Single().RevokedAt.Should().NotBeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Deliver_ios_is_skipped_when_apns_is_disabled()
    {
        await using var db = CreateDb();
        var protector = DataProtectionProvider.Create("IdentityService.Tests.PushDelivery").CreateProtector("HisHope.Mobile.PushToken.v1");
        db.MobileDeviceRegistrations.Add(new MobileDeviceRegistration
        {
            UserId = "user-1", Platform = "ios", TokenCiphertext = protector.Protect("device-token")
        });
        await db.SaveChangesAsync();
        var handler = new RecordingHandler();
        var service = CreateService(db, handler);

        var delivered = await service.DeliverAsync("user-1", "Title", "Body");

        delivered.Should().BeFalse();
        db.PushDeliveryAttempts.Should().BeEmpty();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Deliver_unsupported_platform_is_ignored()
    {
        await using var db = CreateDb();
        var protector = DataProtectionProvider.Create("IdentityService.Tests.PushDelivery").CreateProtector("HisHope.Mobile.PushToken.v1");
        db.MobileDeviceRegistrations.Add(new MobileDeviceRegistration
        {
            UserId = "user-1", Platform = "windows", TokenCiphertext = protector.Protect("device-token")
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var delivered = await service.DeliverAsync("user-1", "Title", "Body");

        delivered.Should().BeFalse();
        db.PushDeliveryAttempts.Should().BeEmpty();
    }

    private static PushDeliveryService CreateService(IdentityDbContext db, RecordingHandler? handler = null, string? firebaseCredentials = null) => new(
        db,
        new TestHttpClientFactory(handler ?? new RecordingHandler()),
        DataProtectionProvider.Create("IdentityService.Tests.PushDelivery"),
        Options.Create(new PushProviderOptions
        {
            ApnsEnabled = false,
            FirebaseCredentialsJson = firebaseCredentials ?? "{\"project_id\":\"demo\",\"client_email\":\"push@example.test\",\"private_key\":\"\"}"
        }),
        new RecordingFirebaseSender(handler ?? new RecordingHandler()),
        NullLogger<PushDeliveryService>.Instance);

    private static IdentityDbContext CreateDb() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static string JsonString(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    private sealed class TestHttpClientFactory(RecordingHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingFirebaseSender(RecordingHandler handler) : IFirebasePushSender
    {
        public async Task SendAsync(string deviceToken, string title, string body, CancellationToken cancellationToken = default)
        {
            using var oauth = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
            using var oauthResponse = await handler.SendAsyncForTest(oauth, cancellationToken);
            if (!oauthResponse.IsSuccessStatusCode) throw new HttpRequestException("Firebase OAuth token request failed.");
            using var message = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/messages:send");
            using var response = await handler.SendAsyncForTest(message, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("Firebase message request failed.");
        }
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Count > 0
                ? _responses.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK));
        }

        public Task<HttpResponseMessage> SendAsyncForTest(HttpRequestMessage request, CancellationToken cancellationToken) => SendAsync(request, cancellationToken);
    }
}
