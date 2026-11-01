using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using His.Hope.Infrastructure.Caching;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace His.Hope.Infrastructure.Security;

public static class RedisDataProtectionExtensions
{
    public static IServiceCollection AddHisHopeRedisDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string applicationName,
        string redisKeyName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(redisKeyName);

        var isDevelopment = environment.IsDevelopment();
        var certificatePath = configuration["DataProtection:CertificatePath"];
        X509Certificate2? certificate = null;

        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            if (!isDevelopment)
                throw new InvalidOperationException("Production DataProtection certificate path is required.");
        }
        else
        {
            if (!File.Exists(certificatePath))
                throw new InvalidOperationException("DataProtection certificate file is missing.");

            var password = configuration["DataProtection:CertificatePassword"];
            try
            {
                certificate = new X509Certificate2(
                    certificatePath,
                    password,
                    X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    "DataProtection certificate is invalid or its password is incorrect.");
            }

            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new InvalidOperationException("DataProtection certificate must include its private key.");
            }

            services.AddSingleton<X509Certificate2>(_ => certificate);
        }

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            if (!isDevelopment)
                throw new InvalidOperationException("Production Redis connection string is required for DataProtection.");

            var keysPath = configuration["DataProtection:KeysPath"]
                ?? Path.Combine(Path.GetTempPath(), "his-hope", applicationName, "data-protection-keys");
            var localDataProtection = services.AddDataProtection()
                .SetApplicationName(applicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
            if (certificate is not null)
                localDataProtection.ProtectKeysWithCertificate(certificate);
            return services;
        }

        var redis = RedisConnectionFactory.Connect(redisConnectionString, configuration);
        services.AddSingleton<IConnectionMultiplexer>(_ => redis);

        var dataProtection = services.AddDataProtection()
            .SetApplicationName(applicationName);

        if (certificate is not null)
            dataProtection.ProtectKeysWithCertificate(certificate);

        dataProtection.PersistKeysToStackExchangeRedis(redis, redisKeyName);

        return services;
    }
}
