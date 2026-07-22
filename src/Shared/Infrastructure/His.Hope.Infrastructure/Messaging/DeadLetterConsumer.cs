using System.Data;
using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace His.Hope.Infrastructure.Messaging;

public class DeadLetterConsumer<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private const string DlxExchangeName = "his-hope.dlx";
    private const string DlqQueueName = "his-hope.dlq";
    private const string AutoReprocessCountHeader = "x-auto-reprocess-count";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadLetterConsumer<TDbContext>> _logger;
    private readonly ConnectionFactory _connectionFactory;
    private readonly bool _autoReprocessEnabled;
    private readonly int _maxRetryCount;
    private readonly int _delayMinutes;
    private readonly string _serviceName;

    private static readonly Meter DlqMeter = new("His.Hope.Infrastructure.Messaging.DeadLetter", "1.0.0");
    private static readonly Counter<int> AutoReprocessCounter = DlqMeter.CreateCounter<int>(
        name: "hishop_dlq_auto_reprocess_total",
        description: "Total number of DLQ auto-reprocess attempts");

    private static string DeriveServiceName()
    {
        var name = typeof(TDbContext).Name;
        return name.EndsWith("DbContext", StringComparison.Ordinal)
            ? name[..^"DbContext".Length].ToLowerInvariant()
            : name.ToLowerInvariant();
    }

    public DeadLetterConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<DeadLetterConsumer<TDbContext>> logger,
        string hostName,
        int port,
        string userName,
        string password,
        string virtualHost = "/",
        bool autoReprocessEnabled = false,
        int maxRetryCount = 3,
        int delayMinutes = 5)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _autoReprocessEnabled = autoReprocessEnabled;
        _maxRetryCount = maxRetryCount;
        _delayMinutes = delayMinutes;
        _serviceName = DeriveServiceName();

        _connectionFactory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadLetterConsumer starting for {DbContext}", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeDeadLettersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogWarning(ex, "RabbitMQ broker unreachable, retrying in 10 seconds");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeadLetterConsumer, retrying in 10 seconds");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ConsumeDeadLettersAsync(CancellationToken stoppingToken)
    {
        using var connection = await Task.Run(() => _connectionFactory.CreateConnection(), stoppingToken);
        using var channel = connection.CreateModel();

        // Declare the DLX topic exchange
        channel.ExchangeDeclare(DlxExchangeName, "topic", durable: true);

        // Declare the DLQ queue with DLX configured (in case of consumer failure)
        var dlqArgs = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = DlxExchangeName,
            ["x-dead-letter-routing-key"] = "dlq.dead-letter"
        };
        channel.QueueDeclare(DlqQueueName, durable: true, exclusive: false, autoDelete: false, arguments: dlqArgs);

        // Bind DLQ to DLX with catch-all routing key
        channel.QueueBind(DlqQueueName, DlxExchangeName, "#");

        // Prefetch 1 to avoid overwhelming the consumer with DLQ messages
        channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            await HandleDeadLetterMessageAsync(channel, args, stoppingToken);
        };

        channel.BasicConsume(DlqQueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation(
            "DeadLetterConsumer listening on queue {DlqQueue} bound to exchange {DlxExchange}",
            DlqQueueName, DlxExchangeName);

        // Block until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private async Task HandleDeadLetterMessageAsync(
        IModel channel,
        BasicDeliverEventArgs args,
        CancellationToken stoppingToken)
    {
        var messageBody = Encoding.UTF8.GetString(args.Body.Span);
        var routingKey = args.RoutingKey;
        var exchange = args.Exchange;
        var messageType = args.BasicProperties.Type ?? "Unknown";
        var messageId = args.BasicProperties.MessageId ?? Guid.NewGuid().ToString();

        // Extract retry count from x-death header if present (set by RabbitMQ when dead-lettered)
        var retryCount = 0;
        Dictionary<string, object>? deathEntry = null;
        if (args.BasicProperties.Headers is { } headers &&
            headers.TryGetValue("x-death", out var deathVal) &&
            deathVal is List<object> deathList &&
            deathList.Count > 0 &&
            deathList[0] is Dictionary<string, object> entry)
        {
            deathEntry = entry;
            if (entry.TryGetValue("count", out var countVal))
                retryCount = Convert.ToInt32(countVal);
        }

        // Use our custom auto-reprocess count if present (tracks total reprocess attempts)
        var autoReprocessCount = 0;
        if (args.BasicProperties.Headers is { } h &&
            h.TryGetValue(AutoReprocessCountHeader, out var arCount) &&
            arCount is not null)
        {
            autoReprocessCount = Convert.ToInt32(arCount);
        }

        var effectiveCount = Math.Max(autoReprocessCount, retryCount);

        _logger.LogCritical(
            "DEAD LETTER MESSAGE - Exchange: {Exchange}, RoutingKey: {RoutingKey}, " +
            "Type: {MessageType}, Id: {MessageId}, RetryCount: {RetryCount}, " +
            "AutoReprocessCount: {AutoReprocessCount}, EffectiveCount: {EffectiveCount}, Body: {Body}",
            exchange, routingKey, messageType, messageId, retryCount, autoReprocessCount, effectiveCount, messageBody);

        // Auto-reprocess if enabled and under max retry count
        if (_autoReprocessEnabled && effectiveCount < _maxRetryCount)
        {
            try
            {
                var originalExchange = deathEntry is not null
                    ? GetDeathHeaderValue(deathEntry, "exchange")
                    : null;
                var originalRoutingKey = deathEntry is not null
                    ? GetFirstDeathRoutingKey(deathEntry)
                    : null;

                // Copy original properties but strip x-death
                var publishProps = channel.CreateBasicProperties();
                publishProps.Persistent = true;
                if (args.BasicProperties.Headers is { } srcHeaders)
                {
                    publishProps.Headers = new Dictionary<string, object>();
                    foreach (var kvp in srcHeaders)
                    {
                        if (kvp.Key != "x-death")
                            publishProps.Headers[kvp.Key] = kvp.Value;
                    }
                }

                // Increment and set auto-reprocess count header
                var newReprocessCount = effectiveCount + 1;
                publishProps.Headers ??= new Dictionary<string, object>();
                publishProps.Headers[AutoReprocessCountHeader] = newReprocessCount;

                // Apply delay via per-message TTL
                if (_delayMinutes > 0)
                    publishProps.Expiration = (_delayMinutes * 60 * 1000).ToString();

                channel.BasicPublish(
                    exchange: originalExchange ?? "his-hope.events",
                    routingKey: originalRoutingKey ?? "#",
                    basicProperties: publishProps,
                    body: args.Body);

                channel.BasicAck(args.DeliveryTag, false);

                AutoReprocessCounter.Add(1,
                    new KeyValuePair<string, object?>("service", _serviceName),
                    new KeyValuePair<string, object?>("status", "succeeded"));

                _logger.LogWarning(
                    "Auto-reprocessed dead letter message {MessageId} back to " +
                    "exchange {Exchange} with routing key {RoutingKey} " +
                    "(attempt {NewCount}/{MaxRetryCount})",
                    messageId, originalExchange ?? "his-hope.events",
                    originalRoutingKey ?? "#", newReprocessCount, _maxRetryCount);

                return;
            }
            catch (Exception ex)
            {
                AutoReprocessCounter.Add(1,
                    new KeyValuePair<string, object?>("service", _serviceName),
                    new KeyValuePair<string, object?>("status", "failed"));

                _logger.LogError(ex,
                    "Failed to auto-reprocess dead letter message {MessageId}, " +
                    "message will remain in DLQ", messageId);

                channel.BasicNack(args.DeliveryTag, false, requeue: false);
                return;
            }
        }

        // Max retries exceeded — persist to dead_letter_messages table
        if (_autoReprocessEnabled && effectiveCount >= _maxRetryCount)
        {
            AutoReprocessCounter.Add(1,
                new KeyValuePair<string, object?>("service", _serviceName),
                new KeyValuePair<string, object?>("status", "exceeded"));

            _logger.LogWarning(
                "Dead letter message {MessageId} exceeded max auto-reprocess count " +
                "({EffectiveCount}/{MaxRetryCount}), persisting to database",
                messageId, effectiveCount, _maxRetryCount);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var connection = context.Database.GetDbConnection();

            await connection.OpenAsync(stoppingToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO dead_letter_messages
                    (original_queue, exchange, routing_key, message_body, message_type,
                     error_message, retry_count, original_message_id, occurred_on, created_at)
                VALUES
                    (@queue, @exchange, @routingKey, @body::jsonb, @messageType,
                     @error, @retryCount, @messageId, @occurredOn, @createdAt)";

            var originalQueueName = routingKey;
            if (deathEntry is not null &&
                deathEntry.TryGetValue("queue", out var queueVal))
            {
                originalQueueName = queueVal?.ToString() ?? originalQueueName;
            }

            AddParameter(cmd, "@queue", DbType.String, originalQueueName);
            AddParameter(cmd, "@exchange", DbType.String, exchange);
            AddParameter(cmd, "@routingKey", DbType.String, routingKey);
            AddParameter(cmd, "@body", DbType.String, messageBody);
            AddParameter(cmd, "@messageType", DbType.String, messageType);
            AddParameter(cmd, "@error", DbType.String, "Message exceeded maximum retry count");
            AddParameter(cmd, "@retryCount", DbType.Int32, retryCount);
            AddParameter(cmd, "@messageId", DbType.String, messageId);
            AddParameter(cmd, "@occurredOn", DbType.DateTime, DateTime.UtcNow);
            AddParameter(cmd, "@createdAt", DbType.DateTime, DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync(stoppingToken);

            _logger.LogInformation(
                "Dead letter message {MessageId} persisted to database", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist dead letter message {MessageId} to database, " +
                "message will remain in DLQ for retry", messageId);

            channel.BasicNack(args.DeliveryTag, false, requeue: false);
            return;
        }

        channel.BasicAck(args.DeliveryTag, false);
    }

    private static string? GetDeathHeaderValue(Dictionary<string, object> deathEntry, string key)
    {
        if (deathEntry.TryGetValue(key, out var val) && val is not null)
        {
            if (val is byte[] bytes)
                return Encoding.UTF8.GetString(bytes);
            return val.ToString();
        }
        return null;
    }

    private static string? GetFirstDeathRoutingKey(Dictionary<string, object> deathEntry)
    {
        if (deathEntry.TryGetValue("routing-keys", out var val) &&
            val is List<object> keys && keys.Count > 0)
        {
            if (keys[0] is byte[] keyBytes)
                return Encoding.UTF8.GetString(keyBytes);
            return keys[0]?.ToString();
        }
        return null;
    }

    private static void AddParameter(IDbCommand cmd, string name, DbType type, object? value)
    {
        var param = cmd.CreateParameter();
        param.ParameterName = name;
        param.DbType = type;
        param.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(param);
    }
}
