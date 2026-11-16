using Amazon.S3;
using Amazon.S3.Model;

namespace His.Hope.Infrastructure.ObjectStorage;

internal sealed class S3ObjectStorage(IAmazonS3 client, string bucket, string prefix) : IObjectStorage
{
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var safeKey = $"{prefix}{ObjectStorageKey.NormalizeKey(key)}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = safeKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);
    }

    public async Task<ObjectStorageReadResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetObjectAsync(bucket, $"{prefix}{ObjectStorageKey.NormalizeKey(key)}", cancellationToken);
            return new ObjectStorageReadResult(
                response.ResponseStream,
                response.Headers.ContentType ?? "application/octet-stream",
                response.ContentLength,
                response);
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.NotFound
            || exception.ErrorCode is "NoSuchKey" or "NoSuchObject")
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        await client.DeleteObjectAsync(bucket, $"{prefix}{ObjectStorageKey.NormalizeKey(key)}", cancellationToken);
}
