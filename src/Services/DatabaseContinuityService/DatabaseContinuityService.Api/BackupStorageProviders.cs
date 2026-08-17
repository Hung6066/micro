using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace His.Hope.DatabaseContinuityService;

public sealed record StorageSyncResult(string Provider, bool UsedFallback, string? Warning = null);
public sealed record RetentionCleanupResult(string Provider, int LocalDeleted, int ProviderDeleted, bool UsedFallback, string? Warning = null);

public interface IBackupStorageProvider
{
    string Name { get; }
    bool CanHandle(Uri storageUri);
    Task SyncBeforeRestoreAsync(string database, string localDirectory, Uri storageUri, CancellationToken ct);
    Task SyncAfterBackupAsync(IReadOnlyCollection<string> files, Uri storageUri, CancellationToken ct);
    Task<int> DeleteExpiredAsync(Uri storageUri, DateTimeOffset cutoff, CancellationToken ct);
}

public sealed class LocalBackupStorageProvider : IBackupStorageProvider
{
    public string Name => "local";

    public bool CanHandle(Uri storageUri) => storageUri.Scheme is "file" or "";

    public Task SyncBeforeRestoreAsync(string database, string localDirectory, Uri storageUri, CancellationToken ct) => Task.CompletedTask;

    public Task SyncAfterBackupAsync(IReadOnlyCollection<string> files, Uri storageUri, CancellationToken ct) => Task.CompletedTask;

    public Task<int> DeleteExpiredAsync(Uri storageUri, DateTimeOffset cutoff, CancellationToken ct) => Task.FromResult(0);
}

public sealed class S3CompatibleBackupStorageProvider(
    IServiceProvider services,
    IOptions<DatabaseContinuityOptions> options,
    ILogger<S3CompatibleBackupStorageProvider> logger) : IBackupStorageProvider
{
    public string Name => "s3-compatible";

    public bool CanHandle(Uri storageUri) => storageUri.Scheme.Equals("s3", StringComparison.OrdinalIgnoreCase);

    public async Task SyncBeforeRestoreAsync(string database, string localDirectory, Uri storageUri, CancellationToken ct)
    {
        var bucket = GetBucket(storageUri);
        var prefix = GetPrefix(storageUri);
        var response = await Client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = Join(prefix, $"{database}-"),
        }, ct);
        var backup = response.S3Objects
            .Where(x => x.Key.EndsWith(".dump.vault", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (backup is null) throw new FileNotFoundException($"No remote backup found for {database}.");

        Directory.CreateDirectory(localDirectory);
        await DownloadAsync(bucket, backup.Key, Path.Combine(localDirectory, Path.GetFileName(backup.Key)), ct);
        var manifestKey = $"{backup.Key}.manifest.json";
        var manifestPath = Path.Combine(localDirectory, Path.GetFileName(manifestKey));
        try { await DownloadAsync(bucket, manifestKey, manifestPath, ct); }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { logger.LogWarning("Remote manifest missing for {Key}", backup.Key); }
    }

    public async Task SyncAfterBackupAsync(IReadOnlyCollection<string> files, Uri storageUri, CancellationToken ct)
    {
        var bucket = GetBucket(storageUri);
        var prefix = GetPrefix(storageUri);
        foreach (var file in files.Where(File.Exists))
        {
            await using var stream = File.OpenRead(file);
            await Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = Join(prefix, Path.GetFileName(file)),
                InputStream = stream,
                AutoCloseStream = false,
                AutoResetStreamPosition = false,
            }, ct);
        }
    }

    public async Task<int> DeleteExpiredAsync(Uri storageUri, DateTimeOffset cutoff, CancellationToken ct)
    {
        var bucket = GetBucket(storageUri);
        var prefix = GetPrefix(storageUri);
        var objects = new List<S3Object>();
        string? token = null;
        do
        {
            var response = await Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix,
                ContinuationToken = token,
            }, ct);
            objects.AddRange(response.S3Objects);
            token = response.IsTruncated ? response.NextContinuationToken : null;
        } while (token is not null);

        var keep = objects
            .Where(x => x.Key.EndsWith(".dump.vault", StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => DatabaseName(Path.GetFileName(x.Key)))
            .SelectMany(group => group.OrderByDescending(x => x.Key, StringComparer.Ordinal).Take(options.Value.KeepLastBackupsPerDatabase))
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);
        var keys = objects
            .Where(x => x.LastModified < cutoff && x.Key.EndsWith(".dump.vault", StringComparison.OrdinalIgnoreCase) && !keep.Contains(x.Key))
            .SelectMany(x => new[] { x.Key, $"{x.Key}.manifest.json" })
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var chunk in keys.Chunk(1000))
        {
            await Client.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = bucket,
                Objects = chunk.Select(key => new KeyVersion { Key = key }).ToList(),
            }, ct);
        }
        return keys.Count;
    }

    private static string DatabaseName(string fileName)
    {
        var match = Regex.Match(fileName, @"^(?<db>.+)-\d{8}T\d{6}Z\.dump\.vault$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["db"].Value : fileName;
    }

    private async Task DownloadAsync(string bucket, string key, string destination, CancellationToken ct)
    {
        using var response = await Client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct);
        await response.WriteResponseStreamToFileAsync(destination, false, ct);
    }

    private IAmazonS3 Client => services.GetRequiredService<IAmazonS3>();

    private static string GetBucket(Uri uri) => string.IsNullOrWhiteSpace(uri.Host)
        ? throw new InvalidOperationException("S3 storage URI must include a bucket.")
        : uri.Host;

    private static string GetPrefix(Uri uri) => uri.AbsolutePath.Trim('/');

    private static string Join(string prefix, string name) => string.IsNullOrWhiteSpace(prefix) ? name : $"{prefix.TrimEnd('/')}/{name}";
}

public sealed class BackupStorageCoordinator(
    IEnumerable<IBackupStorageProvider> providers,
    IOptions<DatabaseContinuityOptions> options,
    ILogger<BackupStorageCoordinator> logger)
{
    private readonly IReadOnlyList<IBackupStorageProvider> _providers = providers.ToList();

    public async Task<StorageSyncResult> PrepareRestoreAsync(string database, CancellationToken ct)
    {
        var config = options.Value;
        var uri = ParseUri(config.StorageUri);
        var primary = Select(config, uri);
        if (primary is LocalBackupStorageProvider) return new("local", false);
        try
        {
            await primary.SyncBeforeRestoreAsync(database, config.LocalStoragePath, uri, ct);
            return new(primary.Name, false);
        }
        catch (Exception ex) when (config.StorageFallbackEnabled && ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Storage provider {Provider} unavailable; using local fallback for restore of {Database}", primary.Name, database);
            return new("local", true, "storage_fallback_local");
        }
    }

    public async Task<StorageSyncResult> PersistBackupAsync(IReadOnlyCollection<string> files, CancellationToken ct)
    {
        var config = options.Value;
        var uri = ParseUri(config.StorageUri);
        var primary = Select(config, uri);
        if (primary is LocalBackupStorageProvider) return new("local", false);
        try
        {
            await primary.SyncAfterBackupAsync(files, uri, ct);
            return new(primary.Name, false);
        }
        catch (Exception ex) when (config.StorageFallbackEnabled && ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Storage provider {Provider} unavailable; retaining backup in local fallback", primary.Name);
            return new("local", true, "storage_fallback_local");
        }
    }

    public async Task<RetentionCleanupResult> CleanupExpiredAsync(CancellationToken ct)
    {
        var config = options.Value;
        var uri = ParseUri(config.StorageUri);
        var primary = Select(config, uri);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-config.RetentionDays);
        if (primary is LocalBackupStorageProvider)
        {
            var count = await DeleteLocalAsync(config.LocalStoragePath, cutoff, config.KeepLastBackupsPerDatabase, ct);
            return new("local", count, 0, false);
        }

        try
        {
            var providerDeleted = await primary.DeleteExpiredAsync(uri, cutoff, ct);
            var localDeleted = await DeleteLocalAsync(config.LocalStoragePath, cutoff, config.KeepLastBackupsPerDatabase, ct);
            return new(primary.Name, localDeleted, providerDeleted, false);
        }
        catch (Exception ex) when (config.StorageFallbackEnabled && ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Storage provider {Provider} unavailable; retaining local backups during retention cleanup", primary.Name);
            return new("local", 0, 0, true, "retention_provider_unavailable_local_retained");
        }
    }

    private IBackupStorageProvider Select(DatabaseContinuityOptions config, Uri uri)
    {
        if (config.StorageProvider.Equals("local", StringComparison.OrdinalIgnoreCase) || uri.Scheme is "file" or "")
            return _providers.Single(x => x is LocalBackupStorageProvider);
        var provider = _providers.FirstOrDefault(x => x.CanHandle(uri));
        return provider ?? throw new InvalidOperationException($"No backup storage provider supports '{uri.Scheme}'.");
    }

    private static Uri ParseUri(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        ? uri
        : new Uri("file:///var/lib/his-hope/backups");

    private static Task<int> DeleteLocalAsync(string directory, DateTimeOffset cutoff, int keepLast, CancellationToken ct)
    {
        if (!Directory.Exists(directory)) return Task.FromResult(0);
        var keep = Directory.EnumerateFiles(directory)
            .Where(path => path.EndsWith(".dump.vault", StringComparison.OrdinalIgnoreCase))
            .GroupBy(path => DatabaseName(Path.GetFileName(path)))
            .SelectMany(group => group.OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase).Take(keepLast))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(directory).Where(path => path.EndsWith(".dump.vault", StringComparison.OrdinalIgnoreCase)))
        {
            ct.ThrowIfCancellationRequested();
            if (!keep.Contains(path) && File.GetLastWriteTimeUtc(path) < cutoff.UtcDateTime)
            {
                File.Delete(path);
                count++;
                var manifest = $"{path}.manifest.json";
                if (File.Exists(manifest)) { File.Delete(manifest); count++; }
            }
        }
        return Task.FromResult(count);
    }

    private static string DatabaseName(string fileName)
    {
        var match = Regex.Match(fileName, @"^(?<db>.+)-\d{8}T\d{6}Z\.dump\.vault$", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["db"].Value : fileName;
    }
}
