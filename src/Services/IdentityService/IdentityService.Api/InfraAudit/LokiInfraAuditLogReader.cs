using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using His.Hope.IdentityService.Application.HostSecurity;
using His.Hope.IdentityService.Application.InfraAudit;
using Microsoft.Extensions.Options;

namespace His.Hope.IdentityService.Api.InfraAudit;

/// <summary>
/// Reads the openship-access-audit tenant through the OpenShip Loki query relay
/// (GET /loki/api/v1/query_range only, basic auth, mandatory X-Scope-OrgID and limit).
/// The relay credential is never logged; failures surface as <see cref="InfraAuditUnavailableException"/>.
/// </summary>
public sealed class LokiInfraAuditLogReader(
    IHttpClientFactory httpClientFactory,
    IOptions<InfraAuditOptions> options,
    ILogger<LokiInfraAuditLogReader> logger) : IInfraAuditLogReader, ILokiStreamReader
{
    public const string HttpClientName = "infra-audit-loki";
    private readonly InfraAuditOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task<IReadOnlyList<(DateTimeOffset Timestamp, string Line)>> ReadAsync(
        string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken) =>
        (await ReadStreamsAsync(_options.Tenant, logQl, from, to, limit, cancellationToken))
            .Select(line => (line.Timestamp, line.Line)).ToList();

    /// <summary>Same relay, any tenant the relay user may read; lines keep their stream labels.</summary>
    public async Task<IReadOnlyList<LokiLine>> ReadStreamsAsync(
        string tenant, string logQl, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InfraAuditUnavailableException("not_configured");
        if (string.IsNullOrWhiteSpace(tenant)) throw new InfraAuditUnavailableException("no_tenant");

        var url = $"{_options.QueryRangeUrl}?query={Uri.EscapeDataString(logQl)}" +
                  $"&start={ToNanoseconds(from)}&end={ToNanoseconds(to)}" +
                  $"&limit={limit.ToString(CultureInfo.InvariantCulture)}&direction=backward";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}")));
        request.Headers.Add("X-Scope-OrgID", tenant);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, timeout.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Infra audit log query failed: {Reason}", exception.GetType().Name);
            throw new InfraAuditUnavailableException("upstream_unreachable");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Infra audit log query returned HTTP {Status}", (int)response.StatusCode);
                throw new InfraAuditUnavailableException($"upstream_{(int)response.StatusCode}");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseStreams(document.RootElement);
        }
    }

    /// <summary>Parses a Loki streams response into (timestamp, line) pairs, newest first.</summary>
    public static IReadOnlyList<(DateTimeOffset Timestamp, string Line)> Parse(JsonElement root) =>
        ParseStreams(root).Select(line => (line.Timestamp, line.Line)).ToList();

    /// <summary>Parses a Loki streams response into lines with their stream labels, newest first.</summary>
    public static IReadOnlyList<LokiLine> ParseStreams(JsonElement root)
    {
        var lines = new List<LokiLine>();
        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Array)
            return lines;
        foreach (var stream in result.EnumerateArray())
        {
            if (!stream.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array) continue;
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            if (stream.TryGetProperty("stream", out var labelObject) && labelObject.ValueKind == JsonValueKind.Object)
                foreach (var label in labelObject.EnumerateObject())
                    if (label.Value.ValueKind == JsonValueKind.String) labels[label.Name] = label.Value.GetString()!;
            foreach (var value in values.EnumerateArray())
            {
                if (value.GetArrayLength() < 2) continue;
                if (!long.TryParse(value[0].GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var ns)) continue;
                var line = value[1].GetString();
                if (line is null) continue;
                lines.Add(new LokiLine(DateTimeOffset.UnixEpoch.AddTicks(ns / 100), line, labels));
            }
        }
        lines.Sort((left, right) => right.Timestamp.CompareTo(left.Timestamp));
        return lines;
    }

    private static string ToNanoseconds(DateTimeOffset value) =>
        ((value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100).ToString(CultureInfo.InvariantCulture);

    /// <summary>Validates the relay against a pinned CA when one is configured.</summary>
    public static HttpMessageHandler CreateHandler(InfraAuditOptions options)
    {
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        X509Certificate2 ca;
        if (!string.IsNullOrWhiteSpace(options.CaCertificatePath) && File.Exists(options.CaCertificatePath))
            ca = X509Certificate2.CreateFromPemFile(options.CaCertificatePath);
        else if (!string.IsNullOrWhiteSpace(options.CaCertificatePem))
            ca = X509Certificate2.CreateFromPem(options.CaCertificatePem.Replace("\\n", "\n", StringComparison.Ordinal));
        else
            return handler;
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, presented, errors) =>
        {
            if (certificate is null) return false;
            if (errors == System.Net.Security.SslPolicyErrors.None) return true;
            if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            // The relay certificate is issued by an intermediate that the server sends
            // with the leaf; it must be available to the chain builder.
            if (presented is not null)
                foreach (var element in presented.ChainElements)
                    chain.ChainPolicy.ExtraStore.Add(element.Certificate);
            return chain.Build(new X509Certificate2(certificate));
        };
        return handler;
    }
}

public sealed class InfraAuditUnavailableException(string reason) : LokiUnavailableException(reason);
