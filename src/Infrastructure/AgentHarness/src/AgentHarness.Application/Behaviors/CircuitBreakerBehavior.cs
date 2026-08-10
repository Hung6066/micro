using Polly.CircuitBreaker;

namespace His.Hope.AgentHarness.Application.Behaviors;

public class CircuitBreakerBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

    public CircuitBreakerBehavior()
    {
        _circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                3,
                TimeSpan.FromMinutes(5),
                (exception, duration) =>
                {
                    Console.WriteLine($"[CircuitBreaker] Circuit broken! Break duration: {duration.TotalSeconds}s. Exception: {exception.Message}");
                },
                () =>
                {
                    Console.WriteLine("[CircuitBreaker] Circuit reset! Back to normal operation.");
                },
                () =>
                {
                    Console.WriteLine("[CircuitBreaker] Circuit half-open! Probing service availability.");
                }
            );
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        return await _circuitBreaker.ExecuteAsync(async ct => await next(), cancellationToken);
    }
}
