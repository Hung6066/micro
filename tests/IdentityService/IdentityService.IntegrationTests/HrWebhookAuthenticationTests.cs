using System.Net;
using His.Hope.IdentityService.Api.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace His.Hope.IdentityService.IntegrationTests;

public class HrWebhookAuthenticationTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";
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
    public async Task AuthenticateAsync_RejectsExpiredTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString();
        var signature = HrWebhookAuthenticator.ComputeSignature(Secret, timestamp, Body);
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
        var signature = HrWebhookAuthenticator.ComputeSignature(Secret, timestamp, Body);
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
        var signature = "sha256=" + HrWebhookAuthenticator.ComputeSignature(Secret, timestamp, Body);
        var request = CreateRequest(timestamp, signature, "evt-1");

        var result = await HrWebhookAuthenticator.AuthenticateAsync(
            request,
            Body,
            CreateConfiguration(),
            new TestReplayStore());

        Assert.True(result.Succeeded);
        Assert.Equal("evt-1", result.EventId);
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
                ["HrWebhook:Secret"] = Secret,
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
