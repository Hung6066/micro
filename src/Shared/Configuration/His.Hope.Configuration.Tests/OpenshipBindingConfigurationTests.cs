using His.Hope.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace His.Hope.Configuration.Tests;

public class OpenshipBindingConfigurationTests
{
    private const string CaPem = "-----BEGIN CERTIFICATE-----\\nMIIBdummy\\n-----END CERTIFICATE-----";
    private const string DatabaseUrl = "postgresql://svc_user:p%40ss%2Fword@db.internal:6543/identity_db?sslmode=verify-full";

    [Fact]
    public void DatabaseUrl_IsConvertedToNpgsqlKeyValueFormat_AndOverridesAppsettingsDefault()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["ConnectionStrings:IdentityDb"] = "Host=localhost;Database=identity",
            ["DATABASE_URL"] = DatabaseUrl,
            ["OPENSHIP_DATABASE_CA_PEM"] = CaPem,
        });

        configuration.ApplyOpenshipBindings("IdentityDb");

        var connectionString = configuration["ConnectionStrings:IdentityDb"]!;
        Assert.Contains("Host=db.internal", connectionString);
        Assert.Contains("Port=6543", connectionString);
        Assert.Contains("Database=identity_db", connectionString);
        Assert.Contains("Username=svc_user", connectionString);
        Assert.Contains("Password=p@ss/word", connectionString);
        Assert.Contains("SSL Mode=VerifyFull", connectionString);
        Assert.DoesNotContain("localhost", connectionString);
        var rootCertificatePath = ExtractRootCertificate(connectionString);
        Assert.NotNull(rootCertificatePath);
        Assert.Contains("BEGIN CERTIFICATE", File.ReadAllText(rootCertificatePath));
    }

    [Fact]
    public void DatabaseUrl_UsesDefaultPort_WhenNoneIsSpecified()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "postgresql://svc:pw@db.internal/app?sslmode=verify-full",
            ["OPENSHIP_DATABASE_CA_PEM"] = CaPem,
        });

        configuration.ApplyOpenshipBindings("IdentityDb");

        Assert.Contains("Port=5432", configuration["ConnectionStrings:IdentityDb"]);
    }

    [Fact]
    public void DatabaseUrl_IsIgnored_WhenNoConnectionNameIsGiven()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = DatabaseUrl,
            ["OPENSHIP_DATABASE_CA_PEM"] = CaPem,
        });

        configuration.ApplyOpenshipBindings(databaseConnectionName: null);

        Assert.Null(configuration["ConnectionStrings:IdentityDb"]);
    }

    [Fact]
    public void DatabaseUrl_WithoutVerifyFull_Throws()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = "postgresql://svc:pw@db.internal:5432/app?sslmode=require",
            ["OPENSHIP_DATABASE_CA_PEM"] = CaPem,
        });

        var ex = Assert.Throws<InvalidOperationException>(() => configuration.ApplyOpenshipBindings("IdentityDb"));
        Assert.Contains("verify-full", ex.Message);
    }

    [Fact]
    public void DatabaseUrl_WithoutCa_Throws()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["DATABASE_URL"] = DatabaseUrl,
        });

        var ex = Assert.Throws<InvalidOperationException>(() => configuration.ApplyOpenshipBindings("IdentityDb"));
        Assert.Contains("OPENSHIP_DATABASE_CA_PEM", ex.Message);
    }

    [Fact]
    public void RedisUrl_IsMappedFromRedisConnectionString_WhenMissing()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = "rediss://u:p@redis.internal:6379",
        });

        configuration.ApplyOpenshipBindings(databaseConnectionName: null);

        Assert.Equal("rediss://u:p@redis.internal:6379", configuration["REDIS_URL"]);
        Assert.Equal("rediss://u:p@redis.internal:6379", configuration["ConnectionStrings:Redis"]);
    }

    [Fact]
    public void RedisUrl_DoesNotOverrideExistingValue()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Redis:ConnectionString"] = "rediss://u:p@redis.internal:6379",
            ["REDIS_URL"] = "rediss://configured:pw@other:6379",
        });

        configuration.ApplyOpenshipBindings(databaseConnectionName: null);

        Assert.Equal("rediss://configured:pw@other:6379", configuration["REDIS_URL"]);
    }

    [Fact]
    public void RabbitMqPemBindings_MaterializeWhenConfiguredPathsDoNotExist()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "openship-pki-missing", Guid.NewGuid().ToString("N"));
        var certificatePath = Path.Combine(missingDirectory, "rabbitmq-client-crt.pem");
        var privateKeyPath = Path.Combine(missingDirectory, "rabbitmq-client-key.pem");
        var caPath = Path.Combine(missingDirectory, "rabbitmq-ca.pem");
        var certificatePem = "-----BEGIN CERTIFICATE-----\nclient\n-----END CERTIFICATE-----";
        var privateKeyPem = "-----BEGIN PRIVATE KEY-----\nprivate\n-----END PRIVATE KEY-----";
        var caPem = "-----BEGIN CERTIFICATE-----\nca\n-----END CERTIFICATE-----";
        var configuration = Build(new Dictionary<string, string?>
        {
            ["EventBus:ClientCertificatePath"] = certificatePath,
            ["EventBus:ClientCertificatePem"] = certificatePem,
            ["EventBus:ClientPrivateKeyPath"] = privateKeyPath,
            ["EventBus:ClientPrivateKeyPem"] = privateKeyPem,
            ["EventBus:CaCertificatePath"] = caPath,
            ["EventBus:CaPem"] = caPem,
        });

        configuration.ApplyOpenshipBindings(databaseConnectionName: null);

        var materializedCertificatePath = configuration["EventBus:ClientCertificatePath"]!;
        var materializedPrivateKeyPath = configuration["EventBus:ClientPrivateKeyPath"]!;
        var materializedCaPath = configuration["EventBus:CaCertificatePath"]!;
        Assert.NotEqual(certificatePath, materializedCertificatePath);
        Assert.NotEqual(privateKeyPath, materializedPrivateKeyPath);
        Assert.NotEqual(caPath, materializedCaPath);
        Assert.Equal(certificatePem.Replace("\\n", "\n", StringComparison.Ordinal), File.ReadAllText(materializedCertificatePath).TrimEnd());
        Assert.Equal(privateKeyPem.Replace("\\n", "\n", StringComparison.Ordinal), File.ReadAllText(materializedPrivateKeyPath).TrimEnd());
        Assert.Equal(caPem.Replace("\\n", "\n", StringComparison.Ordinal), File.ReadAllText(materializedCaPath).TrimEnd());
    }

    private static ConfigurationManager Build(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(values);
        return configuration;
    }

    private static string? ExtractRootCertificate(string connectionString) =>
        connectionString.Split(';')
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0].Trim() == "Root Certificate")
            .Select(pair => pair[1])
            .FirstOrDefault();
}
