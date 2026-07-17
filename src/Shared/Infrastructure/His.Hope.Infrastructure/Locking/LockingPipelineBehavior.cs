using System.Reflection;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace His.Hope.Infrastructure.Locking;

/// <summary>
/// MediatR pipeline behavior that intercepts requests decorated with
/// <see cref="DistributedLockAttribute"/>, acquires a distributed lock
/// before the handler executes, and releases it after completion.
///
/// Placeholders in the resource key (e.g., <c>{TenantId}</c>) are resolved
/// from the request object's public properties at runtime.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public class LockingPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILockManager _lockManager;
    private readonly ILogger<LockingPipelineBehavior<TRequest, TResponse>> _logger;

    public LockingPipelineBehavior(
        ILockManager lockManager,
        ILogger<LockingPipelineBehavior<TRequest, TResponse>> logger)
    {
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var attribute = typeof(TRequest).GetCustomAttribute<DistributedLockAttribute>();

        if (attribute is null)
            return await next().ConfigureAwait(false);

        var resolvedKey = ResolveKeyTemplate(attribute.ResourceKey, request);
        var ttl = TimeSpan.FromSeconds(attribute.TimeoutSeconds);

        _logger.LogDebug(
            "Attempting to acquire distributed lock for key {LockKey} (request: {RequestType})",
            resolvedKey, typeof(TRequest).Name);

        var distributedLock = await _lockManager
            .AcquireAsync(resolvedKey, ttl, cancellationToken)
            .ConfigureAwait(false);

        if (distributedLock is null)
        {
            _logger.LogWarning(
                "Failed to acquire distributed lock for key {LockKey} (request: {RequestType})",
                resolvedKey, typeof(TRequest).Name);

            throw new DistributedLockAcquisitionException(resolvedKey);
        }

        await using (distributedLock.ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Distributed lock acquired for key {LockKey} with fencing token {Token}",
                resolvedKey, distributedLock.FencingToken);

            return await next().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves property placeholders in the key template (e.g., <c>{TenantId}</c>)
    /// by reading the corresponding public property values from the request object.
    /// </summary>
    private static string ResolveKeyTemplate(string template, object request)
    {
        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            var propertyName = match.Groups[1].Value;
            var property = request.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);

            if (property is null)
            {
                // Leave unresolved placeholders as-is for visibility
                return match.Value;
            }

            return property.GetValue(request)?.ToString() ?? string.Empty;
        });
    }
}

/// <summary>
/// Exception thrown when a distributed lock cannot be acquired within the
/// configured timeout, preventing the protected operation from proceeding.
/// </summary>
public sealed class DistributedLockAcquisitionException : InvalidOperationException
{
    /// <summary>The lock resource key that could not be acquired.</summary>
    public string LockKey { get; }

    /// <summary>
    /// Creates a new <see cref="DistributedLockAcquisitionException"/> for the specified lock key.
    /// </summary>
    public DistributedLockAcquisitionException(string lockKey)
        : base($"Failed to acquire distributed lock for resource: {lockKey}")
    {
        LockKey = lockKey;
    }
}
