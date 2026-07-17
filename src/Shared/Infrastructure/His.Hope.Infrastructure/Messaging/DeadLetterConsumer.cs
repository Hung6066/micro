using System.Data;
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

/// <summary>
/// Consumes messages from the Dead Letter Queue (DLQ) and persists them to the
/// dead_letter_messages database table for auditing and manual intervention.
/// 
/// Set up to use the same RabbitMQ connection settings as the event bus.
/// </summary>
public class DeadLetterConsumer<TDbContext> : BackgroundService
    where TDbContext : DbContext
{
    private const string DlxExchangeName = "his-hope.dlx";
    private const string DlqQueueName = "his-hope.dlq";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadLetterConsumer<TDbContext>> _logger;
    private readonly ConnectionFactory _connectionFactory;

    public DeadLetterConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<DeadLetterConsumer<TDbContext>> logger,
        string hostName,
        int port,
        string userName,
        string password,
        string virtualHost = "/")
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

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
        if (args.BasicProperties.Headers is { } headers &&
            headers.TryGetValue("x-death", out var deathVal) &&
            deathVal is List<object> deathList &&
            deathList.Count > 0 &&
            deathList[0] is Dictionary<string, object> deathEntry &&
            deathEntry.TryGetValue("count", out var countVal))
        {
            retryCount = Convert.ToInt32(countVal);
        }

        _logger.LogCritical(
            "DEAD LETTER MESSAGE - Exchange: {Exchange}, RoutingKey: {RoutingKey}, " +
            "Type: {MessageType}, Id: {MessageId}, RetryCount: {RetryCount}, Body: {Body}",
            exchange, routingKey, messageType, messageId, retryCount, messageBody);

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

            var originalQueueName = args.RoutingKey;
            if (args.BasicProperties.Headers is { } h &&
                h.TryGetValue("x-death", out var deathVal2) &&
                deathVal2 is List<object> deathList2 &&
                deathList2.Count > 0 &&
                deathList2[0] is Dictionary<string, object> deathEntry2 &&
                deathEntry2.TryGetValue("queue", out var queueVal))
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

            // Nack without requeue to prevent infinite loop - message stays in DLQ
            // If the consumer restarts, it will try again
            channel.BasicNack(args.DeliveryTag, false, requeue: false);
            return;
        }

        channel.BasicAck(args.DeliveryTag, false);
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
