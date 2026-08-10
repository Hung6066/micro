using System.Threading.Channels;
using SystemDashboard.Bff.Models;

namespace SystemDashboard.Bff.Services;

public sealed class AuditEventWriter : BackgroundService
{
    private readonly ChannelReader<AuditEvent> _reader;
    private readonly ILogger<AuditEventWriter> _logger;

    public AuditEventWriter(
        Channel<AuditEvent> channel,
        ILogger<AuditEventWriter> logger)
    {
        _reader = channel.Reader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AuditEvent>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                await foreach (var auditEvent in _reader.ReadAllAsync(cts.Token))
                {
                    batch.Add(auditEvent);
                    if (batch.Count >= 50)
                        break;
                }
            }
            catch (OperationCanceledException) { }

            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
    }

    private async Task FlushBatchAsync(List<AuditEvent> batch, CancellationToken ct)
    {
        var index = $"his-hope-audit-{DateTime.UtcNow:yyyy.MM.dd}";
        try
        {
            var esUrl = $"http://elasticsearch:9200/{index}/_bulk";
            using var httpClient = new HttpClient();
            var bulkBody = string.Join("\n", batch.Select(e =>
                $"{{\"index\":{{\"_index\":\"{index}\"}}}}\n" +
                System.Text.Json.JsonSerializer.Serialize(e)));

            var content = new StringContent(bulkBody + "\n", System.Text.Encoding.UTF8, "application/x-ndjson");
            var response = await httpClient.PostAsync(esUrl, content, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Audit batch write failed: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit batch write exception");
        }
    }
}
