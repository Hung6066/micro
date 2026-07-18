using Microsoft.AspNetCore.Builder;

namespace His.Hope.Infrastructure.AsyncOperations;

/// <summary>
/// Extension methods for registering the async operation middleware
/// in the ASP.NET pipeline.
/// </summary>
public static class AsyncOperationMiddlewareExtensions
{
    /// <summary>
    /// Adds the <see cref="AsyncOperationMiddleware"/> to the pipeline.
    /// Should be placed early (after error handling and correlation ID)
    /// so it can intercept POST/PUT requests before they reach handlers.
    /// </summary>
    public static IApplicationBuilder UseAsyncOperations(this IApplicationBuilder app) =>
        app.UseMiddleware<AsyncOperationMiddleware>();
}
