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

    public IModel CreateChannel() => GetConnectionAsync().GetAwaiter().GetResult().CreateModel();

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
                var cert = !string.IsNullOrEmpty(_options.ClientPrivateKeyPath)
                    ? X509Certificate2.CreateFromPemFile(_options.ClientCertificatePath, _options.ClientPrivateKeyPath)
                    : string.IsNullOrEmpty(_options.ClientCertificatePassword)
                        ? new X509Certificate2(_options.ClientCertificatePath)
                        : new X509Certificate2(_options.ClientCertificatePath, _options.ClientCertificatePassword);

                factory.Ssl.Certs = new X509Certificate2Collection { cert };
                factory.Ssl.CertificateValidationCallback = SslCertificateValidation;
            }

            if (!string.IsNullOrEmpty(_options.CaCertificatePath))
            {
                var roots = new X509Certificate2Collection();
                roots.ImportFromPemFile(_options.CaCertificatePath);
                factory.Ssl.CertificateValidationCallback = (_, certificate, _, errors) =>
                    ValidateAgainstPrivateCa(roots, certificate, errors);
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

    private static bool ValidateAgainstPrivateCa(
        X509Certificate2Collection roots, X509Certificate? certificate, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None) return true;
        if (certificate is null) return false;
        if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None) return false;

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.AddRange(roots);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(new X509Certificate2(certificate));
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
