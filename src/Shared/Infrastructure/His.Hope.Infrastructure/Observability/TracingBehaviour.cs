using System.Diagnostics;
using MediatR;

namespace His.Hope.Infrastructure.Observability;

public class TracingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ActivitySource _source = new("His.Hope.MediatR");

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        using var activity = _source.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("request.type", name);
        activity?.SetTag("correlation.id", CorrelationContext.CurrentId);

        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetTag("exception.stacktrace", ex.ToString());
            throw;
        }
    }
}
