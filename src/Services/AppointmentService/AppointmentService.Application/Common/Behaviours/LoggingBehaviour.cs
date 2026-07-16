using MediatR;
using Microsoft.Extensions.Logging;

namespace His.Hope.AppointmentService.Application.Common.Behaviours;

public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger) =>
        _logger = logger;

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Processing request: {Name}", requestName);

        try
        {
            var response = await next();
            _logger.LogInformation("Completed request: {Name}", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed request: {Name}", requestName);
            throw;
        }
    }
}
