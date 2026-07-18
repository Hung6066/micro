namespace His.Hope.AgentHarness.Application.Behaviors;

public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly Polly.Retry.AsyncRetryPolicy _retryPolicy;

    public RetryBehavior()
    {
        _retryPolicy = Policy
            .Handle<TimeoutException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) - 1),
                (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"[Retry] Attempt {retryCount}/3 failed. Retrying after {timeSpan.TotalSeconds}s. Exception: {exception.Message}");
                }
            );
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(async ct => await next(), cancellationToken);
    }
}
