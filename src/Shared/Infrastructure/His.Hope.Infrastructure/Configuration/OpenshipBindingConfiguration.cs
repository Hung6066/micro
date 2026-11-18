using Microsoft.Extensions.Configuration;
using Npgsql;

namespace His.Hope.Infrastructure.Configuration;

/// <summary>
/// Maps the environment keys injected by Openship service bindings onto the keys the platform reads.
/// Values the application already configures win, except the managed database URL, which replaces
/// appsettings defaults because the platform binding is the authoritative database for the service.
/// </summary>
public static class OpenshipBindingConfiguration
{
    private const string BindingDirectoryName = "openship-binding";

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

        foreach (var (key, value) in mapped)
            configuration[key] = value;

        return configuration;
    }

    private static void MapDatabaseUrl(IConfiguration configuration, Dictionary<string, string> mapped, string targetKey)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return;

        mapped[targetKey] = ToNpgsqlConnectionString(databaseUrl, configuration["OPENSHIP_DATABASE_CA_PEM"],
            configuration["PLATFORM_PKI_FILE_PG_CLIENT_CRT"], configuration["PLATFORM_PKI_FILE_PG_CLIENT_KEY"]);
    }

    /// <summary>Npgsql does not parse postgresql:// URLs, so the managed database URL is converted to key-value form.</summary>
    private static string ToNpgsqlConnectionString(string databaseUrl, string? caPem, string? clientCertificatePem, string? clientKeyPem)
    {
        if (string.IsNullOrWhiteSpace(caPem))
            throw new InvalidOperationException("OPENSHIP_DATABASE_CA_PEM is required to verify the database certificate.");

        var normalizedCaPem = caPem.Replace("\\n", "\n");
        NpgsqlConnectionStringBuilder builder;
        if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("postgresql" or "postgres"))
                throw InvalidDatabaseUrl();

            var userInfo = uri.UserInfo.Split(':', 2);
            if (userInfo.Length != 2 || string.IsNullOrWhiteSpace(uri.Host) ||
                string.IsNullOrWhiteSpace(uri.AbsolutePath.TrimStart('/')))
                throw InvalidDatabaseUrl();
            if (!HasQueryValue(uri.Query, "sslmode", "verify-full"))
                throw new InvalidOperationException("DATABASE_URL must use sslmode=verify-full.");

            builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = Uri.UnescapeDataString(userInfo[1]),
                SslMode = SslMode.VerifyFull,
            };
        }
        else
        {
            try
            {
                builder = new NpgsqlConnectionStringBuilder(databaseUrl);
            }
            catch (ArgumentException)
            {
                throw InvalidDatabaseUrl();
            }

            if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Database) ||
                string.IsNullOrWhiteSpace(builder.Username) || string.IsNullOrWhiteSpace(builder.Password))
                throw InvalidDatabaseUrl();
            if (builder.SslMode != SslMode.VerifyFull)
                throw new InvalidOperationException("DATABASE_URL must use sslmode=verify-full.");
        }

        builder.SslMode = SslMode.VerifyFull;
        builder.RootCertificate = WritePemFile("openship-postgres-ca.pem", normalizedCaPem);
        if (!string.IsNullOrWhiteSpace(clientCertificatePem) || !string.IsNullOrWhiteSpace(clientKeyPem))
        {
            if (string.IsNullOrWhiteSpace(clientCertificatePem) || string.IsNullOrWhiteSpace(clientKeyPem))
                throw new InvalidOperationException("PostgreSQL mTLS requires both client certificate and key.");
            builder.SslCertificate = WritePemFile("openship-postgres-client.pem", clientCertificatePem.Replace("\\n", "\n"));
            builder.SslKey = WritePemFile("openship-postgres-client-key.pem", clientKeyPem.Replace("\\n", "\n"));
        }
        return builder.ConnectionString;
    }

    private static InvalidOperationException InvalidDatabaseUrl() =>
        new("DATABASE_URL must be a valid PostgreSQL URL or Npgsql connection string with host, database, user, and password.");

    private static bool HasQueryValue(string query, string key, string expected) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Any(pair => pair.Length == 2 &&
                string.Equals(Uri.UnescapeDataString(pair[0]), key, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Uri.UnescapeDataString(pair[1]), expected, StringComparison.OrdinalIgnoreCase));

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
        if (string.IsNullOrWhiteSpace(pem))
            return;

        var configuredPath = configuration[pathKey];
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return;

        // Binding values may carry escaped newlines when they come from a single-line env var.
        // A configured path can outlive the file it named; materialize the bound PEM and point
        // startup at the private fallback file instead of leaving a broken path in configuration.
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
