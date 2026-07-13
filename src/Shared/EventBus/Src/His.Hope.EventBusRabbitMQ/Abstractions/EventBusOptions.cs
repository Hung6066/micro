namespace His.Hope.EventBusRabbitMQ.Abstractions;

public class EventBusOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "admin";
    public string Password { get; set; } = "admin";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "his_hope_exchange";
    public string ExchangeType { get; set; } = "direct";
    public int RetryCount { get; set; } = 5;
    public int PrefetchCount { get; set; } = 10;
    public bool UseSsl { get; set; } = false;
    public string? SslServerName { get; set; }
    public string? ClientCertificatePath { get; set; }
    public string? ClientCertificatePassword { get; set; }
}
