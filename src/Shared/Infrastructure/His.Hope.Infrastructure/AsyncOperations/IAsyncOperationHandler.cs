namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Defines a handler that executes a long-running operation asynchronously.
///
/// Services implement this interface for each type of long-running operation
/// they support (e.g. patient import, report generation). The middleware
/// discovers handlers via DI and invokes them on a background thread pool.
/// </summary>
/// <typeparam name="TRequest">The request DTO type.</typeparam>
/// <typeparam name="TResult">The result DTO type (returned when the operation completes).</typeparam>
public interface IAsyncOperationHandler<TRequest, TResult>
{
    /// <summary>
    /// Executes the long-running operation.
    /// </summary>
    /// <param name="request">The request payload.</param>
    /// <param name="progress">A progress reporter; the handler should call
    /// <c>progress.Report(value)</c> with values 0–100 as work progresses.</param>
    /// <param name="cancellationToken">Cancellation notification.</param>
    /// <returns>The operation result.</returns>
    Task<TResult> ExecuteAsync(TRequest request, IProgress<int> progress, CancellationToken cancellationToken);
}
