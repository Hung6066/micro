using Polly.Timeout;

namespace His.Hope.AgentHarness.Application.Behaviors;

public class TimeoutBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly AsyncTimeoutPolicy _timeoutPolicy;

    public TimeoutBehavior()
    {
        _timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromMinutes(15), TimeoutStrategy.Pessimistic);
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        return await _timeoutPolicy.ExecuteAsync(async ct => await next(), cancellationToken);
    }
}
