namespace His.Hope.Infrastructure.ObjectStorage;

public interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<ObjectStorageReadResult?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class ObjectStorageReadResult : IAsyncDisposable
{
    private readonly IDisposable? _owner;

    internal ObjectStorageReadResult(Stream content, string contentType, long? contentLength, IDisposable? owner = null)
    {
        Content = content;
        ContentType = contentType;
        ContentLength = contentLength;
        _owner = owner;
    }

    public Stream Content { get; }
    public string ContentType { get; }
    public long? ContentLength { get; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        _owner?.Dispose();
    }
}
