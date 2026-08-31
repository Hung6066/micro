namespace His.Hope.Configuration;

/// <summary>
/// Canonical configuration paths shared by hosts and infrastructure.
/// Service-specific options belong to the service and must not be added here.
/// </summary>
public static class HisHopeConfigurationKeys
{
    public static class EventBus
    {
        public const string HostName = "EventBus:HostName";
        public const string Port = "EventBus:Port";
        public const string UserName = "EventBus:UserName";
        public const string Password = "EventBus:Password";
        public const string VirtualHost = "EventBus:VirtualHost";
        public const string InternalExchangeName = "EventBus:InternalExchangeName";
        public const string PublisherChannelPoolSize = "EventBus:PublisherChannelPoolSize";
        public const string PublisherConfirmTimeoutMilliseconds = "EventBus:PublisherConfirmTimeoutMilliseconds";
        public const string UseSsl = "EventBus:UseSsl";
        public const string ClientCertificatePath = "EventBus:ClientCertificatePath";
        public const string ClientCertificatePassword = "EventBus:ClientCertificatePassword";
    }

    public static class Gateway
    {
        public const string RequireHttps = "Gateway:RequireHttps";
    }

    public static class Certificates
    {
        public const string Path = "Certificates:Path";
        public const string Password = "Certificates:Password";
    }

    public const string RedisConnectionString = "Redis:ConnectionString";
    public const string DataProtectionKeyName = "DataProtection:KeyName";
    public const string CorsAllowedOrigins = "CORS:AllowedOrigins";
    public const string FeatureManagementUnleash = "FeatureManagement:Unleash";
    public const string RuntimeEnvironment = "HIS_HOPE_ENVIRONMENT";
    public const string Plugins = "Plugins";
}
