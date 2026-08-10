using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Resolves the correct <see cref="IAsyncOperationHandler{TRequest,TResult}"/>
/// for a given <see cref="AsyncOperationWorkItem"/> based on its
/// <see cref="AsyncOperationWorkItem.OperationType"/>.
///
/// Services register their handlers at startup:
/// <code>
/// services.AddAsyncOperationHandler&lt;PatientImportHandler, ImportRequest, ImportResult&gt;("PatientImport");
/// </code>
/// </summary>
public class AsyncOperationHandlerResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AsyncOperationHandlerResolver> _logger;
    private readonly ConcurrentDictionary<string, HandlerRegistration> _registrations = new();

    public AsyncOperationHandlerResolver(
        IServiceProvider serviceProvider,
        ILogger<AsyncOperationHandlerResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Registers a handler for the given operation type.
    /// </summary>
    public void Register<TRequest, TResult>(
        string operationType,
        IAsyncOperationHandler<TRequest, TResult>? instance = null)
        where TRequest : class
        where TResult : class
    {
        _registrations[operationType] = new HandlerRegistration
        {
            OperationType = operationType,
            HandlerType = typeof(IAsyncOperationHandler<TRequest, TResult>),
            RequestType = typeof(TRequest),
            ResultType = typeof(TResult),
            Instance = instance
        };

        _logger.LogDebug(
            "Registered async operation handler for '{OperationType}': {HandlerType}",
            operationType, typeof(IAsyncOperationHandler<TRequest, TResult>).Name);
    }

    /// <summary>
    /// Resolves and executes the handler for the given work item.
    /// </summary>
    public async Task<object?> ExecuteAsync(
        AsyncOperationWorkItem workItem,
        IProgress<int> progress,
        CancellationToken ct)
    {
        if (!_registrations.TryGetValue(workItem.OperationType, out var registration))
        {
            throw new InvalidOperationException(
                $"No handler registered for operation type '{workItem.OperationType}'.");
        }

        // Resolve the handler instance
        object handlerInstance;
        if (registration.Instance is not null)
        {
            handlerInstance = registration.Instance;
        }
        else
        {
            handlerInstance = _serviceProvider.GetRequiredService(registration.HandlerType);
        }

        // Deserialize the request
        var request = JsonConvert.DeserializeObject(
            workItem.RequestData ?? "{}",
            registration.RequestType);

        if (request is null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize request data for operation type '{workItem.OperationType}'.");
        }

        // Get the ExecuteAsync method via reflection
        var executeMethod = registration.HandlerType.GetMethod(nameof(IAsyncOperationHandler<object, object>.ExecuteAsync));
        if (executeMethod is null)
        {
            throw new InvalidOperationException(
                $"Could not resolve ExecuteAsync method on handler for '{workItem.OperationType}'.");
        }

        // Invoke and await
        var task = (Task)executeMethod.Invoke(handlerInstance, [request, progress, ct])!;
        await task.ConfigureAwait(false);

        // Extract the result
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    private sealed class HandlerRegistration
    {
        public string OperationType { get; init; } = string.Empty;
        public Type HandlerType { get; init; } = null!;
        public Type RequestType { get; init; } = null!;
        public Type ResultType { get; init; } = null!;
        public object? Instance { get; init; }
    }
}
