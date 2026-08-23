using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using His.Hope.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace His.Hope.IdentityService.Infrastructure.Services;

/// <summary>
/// Forwards audit records to external SIEM and WORM endpoints when configured.
/// Uses a hash chain for tamper-evidence on each delivery attempt.
/// </summary>
public sealed class SiemWormAuditForwarder
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SiemWormAuditForwarder> _logger;
    private readonly ConcurrentQueue<object> _deadLetter = new();
    private int _consecutiveFailures;

    public IReadOnlyCollection<object> DeadLetter => _deadLetter.ToArray();

    public int ConsecutiveDeliveryFailures => Volatile.Read(ref _consecutiveFailures);

    public SiemWormAuditForwarder(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SiemWormAuditForwarder> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask ForwardAsync(AuditRecord auditRecord, CancellationToken cancellationToken = default)
    {
        var siemUrl = _configuration["AUDIT_SIEM_URL"];
        var wormEndpoint = _configuration["AUDIT_WORM_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(siemUrl) && string.IsNullOrWhiteSpace(wormEndpoint))
            return;

        var envelope = new
        {
            auditRecord.Action,
            auditRecord.Resource,
            auditRecord.SubjectId,
            auditRecord.OccurredAt,
            auditRecord.Properties,
            previousHash = _previousHash,
            chainHash = ComputeChainHash(auditRecord)
        };
        _previousHash = envelope.chainHash;

        var delivered = true;
        if (!string.IsNullOrWhiteSpace(siemUrl))
            delivered &= await TryDeliverAsync(client => client.PostAsJsonAsync(siemUrl, envelope, cancellationToken), "SIEM", cancellationToken);

        if (!string.IsNullOrWhiteSpace(wormEndpoint))
        {
            var bucket = _configuration["AUDIT_WORM_BUCKET"] ?? "his-hope-audit";
            var wormUrl = wormEndpoint.TrimEnd('/') + $"/{bucket}/{auditRecord.OccurredAt:yyyy/MM/dd}/{Guid.NewGuid():N}.json";
            delivered &= await TryDeliverAsync(client => client.PutAsJsonAsync(wormUrl, envelope, cancellationToken), "WORM", cancellationToken);
        }

        if (delivered)
            Interlocked.Exchange(ref _consecutiveFailures, 0);
        else
            Interlocked.Increment(ref _consecutiveFailures);
    }

    private async Task<bool> TryDeliverAsync(
        Func<HttpClient, Task<HttpResponseMessage>> send,
        string sinkName,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(SiemWormAuditForwarder));
        client.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            using var response = await send(client);
            if (response.IsSuccessStatusCode)
                return true;

            _logger.LogWarning("{Sink} audit delivery failed with status {StatusCode}", sinkName, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "{Sink} audit delivery failed.", sinkName);
        }

        _deadLetter.Enqueue(new { sink = sinkName, failedAtUtc = DateTimeOffset.UtcNow });
        return false;
    }

    private string? _previousHash;

    private string ComputeChainHash(AuditRecord auditRecord)
    {
        var payload = JsonSerializer.Serialize(new
        {
            auditRecord.Action,
            auditRecord.Resource,
            auditRecord.SubjectId,
            auditRecord.OccurredAt,
            previousHash = _previousHash
        });
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}

/// <summary>Durable audit sink that persists locally and forwards to SIEM/WORM when configured.</summary>
public sealed class IdentityDurableAuditSink : IDurableAuditSink
{
    private readonly IdentityObservabilityAuditSink _inner;
    private readonly SiemWormAuditForwarder _forwarder;

    public IdentityDurableAuditSink(IdentityObservabilityAuditSink inner, SiemWormAuditForwarder forwarder)
    {
        _inner = inner;
        _forwarder = forwarder;
    }

    public async ValueTask WriteAsync(AuditRecord auditRecord, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAsync(auditRecord, cancellationToken);
        await _forwarder.ForwardAsync(auditRecord, cancellationToken);
    }
}
