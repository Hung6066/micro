using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace His.Hope.Infrastructure.ObjectStorage;

public static class ObjectStorageServiceCollectionExtensions
{
    public static IServiceCollection AddHisHopeObjectStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var provider = configuration["OBJECT_STORAGE_PROVIDER"]?.Trim().ToLowerInvariant()
            ?? (environment.IsDevelopment() ? "local" : "s3");

        if (provider == "local")
        {
            services.AddSingleton<IObjectStorage>(sp => new LocalObjectStorage(
                configuration["OBJECT_STORAGE_LOCAL_ROOT"]
                    ?? Path.Combine(environment.ContentRootPath, "uploads"),
                configuration["S3_PREFIX"] ?? ""));
            return services;
        }

        if (provider != "s3")
            throw new InvalidOperationException("OBJECT_STORAGE_PROVIDER must be 's3' or 'local'.");

        var endpoint = Required(configuration, "S3_ENDPOINT");
        var region = Required(configuration, "S3_REGION");
        var bucket = Required(configuration, "S3_BUCKET");
        var accessKeyId = Required(configuration, "S3_ACCESS_KEY_ID");
        var secretAccessKey = Required(configuration, "S3_SECRET_ACCESS_KEY");
        var prefix = ObjectStorageKey.NormalizePrefix(configuration["S3_PREFIX"] ?? "");

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
            || endpointUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("S3_ENDPOINT must be an HTTPS URL.");

        var clientConfig = new AmazonS3Config
        {
            ServiceURL = endpointUri.ToString().TrimEnd('/'),
            AuthenticationRegion = region,
            ForcePathStyle = configuration.GetValue("S3_FORCE_PATH_STYLE", true),
        };
        services.AddSingleton<IAmazonS3>(_ =>
            new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), clientConfig));
        services.AddSingleton<IObjectStorage>(sp => new S3ObjectStorage(
            sp.GetRequiredService<IAmazonS3>(), bucket, prefix));
        return services;
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"S3 object storage requires {key}.");
        return value;
    }
}
