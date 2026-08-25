using System.Net;
using System.Reflection;
using His.Hope.IdentityService.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class HrWebhookAuthenticationTests
{
    private const string TestWebhookKey = "test-webhook-signing-key-for-tests";
    private const string Body = "{\"eventType\":\"employee.hired\",\"eventId\":\"evt-1\",\"timestamp\":\"2026-07-23T00:00:00Z\",\"employee\":{\"employeeId\":\"e-1\",\"email\":\"doctor@example.test\"}}";

    [Fact]
    public async Task AuthenticateAsync_RejectsMissingSignature()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = CreateRequest(timestamp, eventId: "evt-1");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Contains("signature", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsWhenSecretIsNotConfigured()
    {
        var request = CreateRequest(DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), eventId: "evt-secret");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            new ConfigurationBuilder().Build(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsMalformedTimestamp()
    {
        var request = CreateRequest("not-a-unix-timestamp", eventId: "evt-time");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsInvalidSignature()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var request = CreateRequest(timestamp, "sha256=invalid", "evt-signature");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsMissingEventId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var request = CreateRequest(timestamp, signature);

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsExpiredTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var request = CreateRequest(timestamp, signature, "evt-1");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("timestamp", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsReplayEventId()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var replayStore = new TestReplayStore();
        var firstRequest = CreateRequest(timestamp, signature, "evt-1");
        var secondRequest = CreateRequest(timestamp, signature, "evt-1");

        var firstResult = await HrWebhookAuthenticator.AuthenticateAsync(
            firstRequest,
            Body,
            CreateConfiguration(),
            replayStore);
        var secondResult = await HrWebhookAuthenticator.AuthenticateAsync(
            secondRequest,
            Body,
            CreateConfiguration(),
            replayStore);

        Assert.True(firstResult.Succeeded);
        Assert.False(secondResult.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, secondResult.StatusCode);
        Assert.Contains("Duplicate", secondResult.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsync_AcceptsValidSignatureWithSha256Prefix()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = "sha256=" + HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var request = CreateRequest(timestamp, signature, "evt-1");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.True(result.Succeeded);
        Assert.Equal("evt-1", result.EventId);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsDuplicateHeaderValues()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var request = CreateRequest(timestamp, signature, "evt-duplicate-header");
        request.Headers.Append(HrWebhookAuthenticator.EventIdHeader, "evt-second-value");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("event id", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsFutureTimestampOutsideTolerance()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            CreateRequest(timestamp, signature, "evt-future"),
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("outside", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsync_RejectsShortSecretAndSupportsLegacyConfigurationKey()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var request = CreateRequest(timestamp, signature, "evt-legacy-key");

        var shortSecret = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HrWebhook:Secret"] = "too-short"
            })
            .Build();
        var rejected = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            shortSecret,
            new TestReplayStore());
        Assert.False(rejected.Succeeded);
        Assert.Equal((int)HttpStatusCode.Unauthorized, rejected.StatusCode);

        var legacy = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HrWebhooks:Secret"] = TestWebhookKey,
                ["HrWebhook:TimestampToleranceSeconds"] = "300"
            })
            .Build();
        var accepted = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            legacy,
            new TestReplayStore());
        Assert.True(accepted.Succeeded);
    }

    [Fact]
    public async Task AuthenticateAsync_AcceptsCaseInsensitivePrefixAndWhitespace()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(TestWebhookKey, timestamp, Body);
        var request = CreateRequest(timestamp, $"SHA256={signature.ToUpperInvariant()}  ", "evt-normalized-signature");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("nursing", "Nurse")]
    [InlineData("LABORATORY", "LabTechnician")]
    [InlineData("pharmacy", "Pharmacist")]
    [InlineData("billing", "BillingClerk")]
    [InlineData("reception", "Receptionist")]
    [InlineData("medical", "Provider")]
    [InlineData("unknown-department", "Provider")]
    [InlineData(null, "Provider")]
    public void MapDepartmentToRole_maps_known_departments_and_defaults(string? department, string expectedRole)
    {
        var method = typeof(HrWebhookEndpoints).GetMethod(
            "MapDepartmentToRole",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var role = method!.Invoke(null, new object?[] { department });
        Assert.Equal(expectedRole, role);
    }

    private static HttpRequest CreateRequest(string timestamp, string? signature = null, string? eventId = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HrWebhookAuthenticator.TimestampHeader] = timestamp;

        if (signature is not null)
            context.Request.Headers[HrWebhookAuthenticator.SignatureHeader] = signature;

        if (eventId is not null)
            context.Request.Headers[HrWebhookAuthenticator.EventIdHeader] = eventId;

        return context.Request;
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HrWebhook:Secret"] = TestWebhookKey,
                ["HrWebhook:TimestampToleranceSeconds"] = "300"
            })
            .Build();

    private sealed class TestReplayStore : IHrWebhookReplayStore
    {
        private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);

        public Task<bool> TryMarkSeenAsync(string eventId, TimeSpan ttl, CancellationToken ct) =>
            Task.FromResult(_eventIds.Add(eventId));
    }
}
