namespace His.Hope.ServiceDefaults;

public sealed class EmailProviderOptions
{
    public const string SectionName = "ExternalProviders:Email";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "noop";
    public string Endpoint { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string ApiKeySecretPath { get; set; } = string.Empty;
    public string ApiKeySecretKey { get; set; } = "api_key";
}

public sealed class SmsProviderOptions
{
    public const string SectionName = "ExternalProviders:Sms";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "noop";
    public string Endpoint { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string ApiKeySecretPath { get; set; } = string.Empty;
    public string ApiKeySecretKey { get; set; } = "api_key";
}

public sealed class FirebaseProviderOptions
{
    public const string SectionName = "ExternalProviders:Firebase";
    public bool Enabled { get; set; }
    public string CredentialsJson { get; set; } = string.Empty;
    public string CredentialsFile { get; set; } = string.Empty;
    public string CredentialsSecretPath { get; set; } = string.Empty;
    public string CredentialsSecretKey { get; set; } = "credentials_json";
}

public sealed class PaymentProviderOptions
{
    public const string SectionName = "ExternalProviders:Payment";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "not-configured";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKeySecretPath { get; set; } = string.Empty;
    public string ApiKeySecretKey { get; set; } = "api_key";
}

public sealed class ShipmentProviderOptions
{
    public const string SectionName = "ExternalProviders:Shipment";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "not-configured";
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKeySecretPath { get; set; } = string.Empty;
    public string ApiKeySecretKey { get; set; } = "api_key";
    public string WebhookSecretPath { get; set; } = string.Empty;
    public string WebhookSecretKey { get; set; } = "webhook_secret";
}
