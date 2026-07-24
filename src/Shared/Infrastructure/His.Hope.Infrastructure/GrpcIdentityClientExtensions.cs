using Grpc.Net.ClientFactory;
using His.Hope.Identity.Grpc;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace His.Hope.Infrastructure;

/// <summary>
/// Registers gRPC IdentityService client with Polly circuit breaker (3 failures → open, 30s break).
/// </summary>
public static class GrpcIdentityClientExtensions
{
    public static IServiceCollection AddHisHopeGrpcIdentityClient(
        this IServiceCollection services,
        string identityServiceUrl)
    {
        services.AddGrpcClient<IdentityService.IdentityServiceClient>(options =>
        {
            options.Address = new Uri(identityServiceUrl);
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                          || msg.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, ts) =>
                {
                    // Circuit breaker opened — identity service callers will fail-closed
                },
                onReset: () =>
                {
                    // Circuit breaker reset — identity service recovered
                }));

        return services;
    }
}
