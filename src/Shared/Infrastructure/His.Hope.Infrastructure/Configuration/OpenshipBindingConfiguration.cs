using Microsoft.Extensions.Configuration;
using Npgsql;

namespace His.Hope.Infrastructure.Configuration;

/// <summary>
/// Maps the environment keys injected by Openship service bindings onto the keys the platform reads.
/// Values the application already configures win, except the managed database URL, which replaces
/// appsettings defaults because the platform binding is the authoritative database for the service.
/// Inline key material (*Pem) replaces the configured key file path, so keys can be supplied without a volume.
/// </summary>
public static class OpenshipBindingConfiguration
{
    private const string BindingDirectoryName = "openship-binding";

    private static readonly (string PathKey, string FileName, string PemKey)[] KeyMaterial =
    [
        ("Jwt:RsaPublicKeyPath", "jwt-public-key.pem", "Jwt:RsaPublicKeyPem"),
        ("Jwt:RsaEncryptionPrivateKeyPath", "jwt-encryption-private-key.pem", "Jwt:RsaEncryptionPrivateKeyPem"),
        ("OpenIddict:Signing:PrivateKeyPath", "openiddict-signing-private-key.pem", "OpenIddict:Signing:PrivateKeyPem"),
        ("OpenIddict:Encryption:PrivateKeyPath", "openiddict-encryption-private-key.pem", "OpenIddict:Encryption:PrivateKeyPem"),
    ];

    public static IConfigurationManager ApplyOpenshipBindings(this IConfigurationManager configuration, string? databaseConnectionName)
    {
        var mapped = new Dictionary<string, string>();

        if (databaseConnectionName is not null)
            MapDatabaseUrl(configuration, mapped, $"ConnectionStrings:{databaseConnectionName}");
        Map(configuration, mapped, "ConnectionStrings:Redis", configuration["Redis:ConnectionString"]);
        Map(configuration, mapped, "REDIS_URL", configuration["Redis:ConnectionString"]);
        Map(configuration, mapped, "EventBus:HostName", configuration["EventBus:Host"]);
        MapPemFile(configuration, mapped, "Redis:TlsCaFile", "redis-ca.pem", "Redis:TlsCaPem");
        MapPemFile(configuration, mapped, "EventBus:CaCertificatePath", "eventbus-ca.pem", "EventBus:CaPem");
        MapPemFile(configuration, mapped, "EventBus:ClientCertificatePath", "eventbus-client.pem", "EventBus:ClientCertificatePem");
        MapPemFile(configuration, mapped, "EventBus:ClientPrivateKeyPath", "eventbus-client-key.pem", "EventBus:ClientPrivateKeyPem");
        foreach (var (pathKey, fileName, pemKey) in KeyMaterial)
            ReplacePemFile(configuration, mapped, pathKey, fileName, pemKey);

        foreach (var (key, value) in mapped)
            configuration[key] = value;

        return configuration;
    }

    private static void MapDatabaseUrl(IConfiguration configuration, Dictionary<string, string> mapped, string targetKey)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return;

        mapped[targetKey] = ToNpgsqlConnectionString(databaseUrl, configuration["OPENSHIP_DATABASE_CA_PEM"]);
    }

    /// <summary>Npgsql does not parse postgresql:// URLs, so the managed database URL is converted to key-value form.</summary>
    private static string ToNpgsqlConnectionString(string databaseUrl, string? caPem)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        if (uri.Scheme is not ("postgresql" or "postgres") || userInfo.Length != 2)
            throw new InvalidOperationException("DATABASE_URL must be a PostgreSQL URL with a user and password.");
        if (!HasQueryValue(uri.Query, "sslmode", "verify-full"))
            throw new InvalidOperationException("DATABASE_URL must use sslmode=verify-full.");
        if (string.IsNullOrWhiteSpace(caPem))
            throw new InvalidOperationException("OPENSHIP_DATABASE_CA_PEM is required to verify the database certificate.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.VerifyFull,
            RootCertificate = WritePemFile("openship-postgres-ca.pem", caPem.Replace("\\n", "\n")),
        };
        return builder.ConnectionString;
    }

    private static bool HasQueryValue(string query, string key, string expected) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Any(pair => pair.Length == 2 && pair[0] == key && pair[1] == expected);

    private static void Map(IConfiguration configuration, Dictionary<string, string> mapped, string targetKey, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(configuration[targetKey]))
            mapped[targetKey] = value;
    }

    private static void MapPemFile(
        IConfiguration configuration,
        Dictionary<string, string> mapped,
        string pathKey,
        string fileName,
        string pemKey)
    {
        var pem = configuration[pemKey];
        if (string.IsNullOrWhiteSpace(pem) || !string.IsNullOrWhiteSpace(configuration[pathKey]))
            return;

        // Binding values may carry escaped newlines when they come from a single-line env var.
        mapped[pathKey] = WritePemFile(fileName, pem.Replace("\\n", "\n"));
    }

    private static void ReplacePemFile(
        IConfiguration configuration,
        Dictionary<string, string> mapped,
        string pathKey,
        string fileName,
        string pemKey)
    {
        var pem = configuration[pemKey];
        if (string.IsNullOrWhiteSpace(pem))
            return;

        mapped[pathKey] = WritePemFile(fileName, pem.Replace("\\n", "\n"));
    }

    private static string WritePemFile(string fileName, string pem)
    {
        var directory = Path.Combine(Path.GetTempPath(), BindingDirectoryName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, pem);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }
}
