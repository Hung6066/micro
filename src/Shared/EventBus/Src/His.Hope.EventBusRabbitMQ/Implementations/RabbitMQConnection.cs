using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using His.Hope.EventBusRabbitMQ.Abstractions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;

namespace His.Hope.EventBusRabbitMQ.Implementations;

public class RabbitMQConnection : IAsyncDisposable
{
    private readonly EventBusOptions _options;
    private readonly ILogger<RabbitMQConnection> _logger;
    private readonly object _lock = new();
    private IConnection? _connection;
    private bool _disposed;

    public bool IsConnected => _connection is { IsOpen: true } && !_disposed;

    public RabbitMQConnection(EventBusOptions options, ILogger<RabbitMQConnection> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync()
    {
        if (IsConnected)
            return _connection!;

        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(_options.RetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (ex, time, retry, _) =>
                {
                    _logger.LogWarning(ex,
                        "RabbitMQ connection failed (attempt {Retry}/{Retries}), retrying in {Time}s",
                        retry, _options.RetryCount, time.TotalSeconds);
                });

        return await retryPolicy.ExecuteAsync(CreateConnectionAsync);
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        if (IsConnected)
            return _connection!;

        lock (_lock)
        {
            if (IsConnected)
                return _connection!;
        }

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        };

        if (_options.UseSsl)
        {
            factory.Ssl.Enabled = true;
            factory.Ssl.ServerName = _options.SslServerName ?? _options.HostName;

            if (!string.IsNullOrEmpty(_options.ClientCertificatePath))
            {
                var cert = string.IsNullOrEmpty(_options.ClientCertificatePassword)
                    ? new X509Certificate2(_options.ClientCertificatePath)
                    : new X509Certificate2(_options.ClientCertificatePath, _options.ClientCertificatePassword);

                factory.Ssl.CertificateCollection = new X509CertificateCollection { cert };
                factory.Ssl.CertificateValidationCallback = SslCertificateValidation;
            }
        }

        _connection = await Task.Run(() => factory.CreateConnection());

        _connection.ConnectionShutdown += (_, args) =>
            _logger.LogWarning("RabbitMQ connection shutdown: {Reason}", args.ReplyText);

        _connection.CallbackException += (_, args) =>
            _logger.LogError(args.Exception, "RabbitMQ callback exception");

        _logger.LogInformation("RabbitMQ connected to {Host}:{Port}", _options.HostName, _options.Port);

        return _connection;
    }

    private static bool SslCertificateValidation(object sender, X509Certificate? certificate,
        X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        return sslPolicyErrors == SslPolicyErrors.None;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            await Task.Run(() => _connection?.Close());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing RabbitMQ connection");
        }

        _connection?.Dispose();
        _connection = null;
    }
}
