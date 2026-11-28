namespace His.Hope.Infrastructure.ObjectStorage;

internal sealed class LocalObjectStorage : IObjectStorage
{
    private readonly string _root;
    private readonly string _prefix;

    public LocalObjectStorage(string root, string prefix)
    {
        _root = Path.GetFullPath(root);
        _prefix = ObjectStorageKey.NormalizePrefix(prefix);
        Directory.CreateDirectory(_root);
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(output, cancellationToken);
    }

    public Task<ObjectStorageReadResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path)) return Task.FromResult<ObjectStorageReadResult?>(null);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<ObjectStorageReadResult?>(
            new ObjectStorageReadResult(stream, ContentTypeFor(path), stream.Length));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var safeKey = $"{_prefix}{ObjectStorageKey.NormalizeKey(key)}";
        var path = Path.GetFullPath(Path.Combine(_root, safeKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Object storage key resolves outside its local root.", nameof(key));
        return path;
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".avif" => "image/avif",
        _ => "application/octet-stream",
    };
}
