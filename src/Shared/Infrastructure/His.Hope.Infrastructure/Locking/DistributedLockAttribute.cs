namespace His.Hope.Infrastructure.Locking;

/// <summary>
/// Decorates a MediatR request (IRequest or ICommand) to automatically acquire
/// a distributed lock before executing the handler and release it after completion.
/// Supports template placeholders like <c>{TenantId}</c>, <c>{PatientId}</c> etc.,
/// which are resolved from the request's public properties at runtime.
/// </summary>
/// <example>
/// <code>
/// [DistributedLock("invoice:{TenantId}:{InvoiceId}", TimeoutSeconds = 30)]
/// public record ProcessInvoiceCommand(Guid TenantId, Guid InvoiceId) : IRequest;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DistributedLockAttribute : Attribute
{
    /// <summary>
    /// The lock resource key template. Use <c>{PropertyName}</c> placeholders
    /// that will be resolved from the request object's properties at runtime.
    /// </summary>
    public string ResourceKey { get; }

    /// <summary>
    /// Maximum time (in seconds) to hold the lock.
    /// Defaults to 30 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Creates a new <see cref="DistributedLockAttribute"/> with the specified resource key template.
    /// </summary>
    /// <param name="resourceKey">
    /// The lock resource key. Use <c>{PropertyName}</c> for property substitution.
    /// Example: <c>"invoice:{TenantId}:{InvoiceId}"</c>
    /// </param>
    public DistributedLockAttribute(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);
        ResourceKey = resourceKey;
    }
}
