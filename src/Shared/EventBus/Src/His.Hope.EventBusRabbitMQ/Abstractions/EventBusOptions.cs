namespace His.Hope.EventBusRabbitMQ.Abstractions;

public class EventBusOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "admin";
    // Credentials are supplied by the runtime secret provider. Never ship a
    // usable broker password as a library default.
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "his_hope_exchange";
    public string ExchangeType { get; set; } = "direct";
    public string ExternalExchangeName { get; set; } = "his_hope_external_exchange";
    public string ExternalExchangeType { get; set; } = "topic";
    public int RetryCount { get; set; } = 5;
    public int PrefetchCount { get; set; } = 10;
    public int PublisherChannelPoolSize { get; set; } = 4;
    public int PublisherConfirmTimeoutMilliseconds { get; set; } = 5000;
    public bool UseSsl { get; set; } = false;
    public string? SslServerName { get; set; }
    public string? ClientCertificatePath { get; set; }
    public string? ClientCertificatePassword { get; set; }
}
