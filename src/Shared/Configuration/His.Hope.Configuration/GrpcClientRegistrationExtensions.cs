using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.Configuration;

public static class GrpcClientRegistrationExtensions
{
    public static IHttpClientBuilder AddHisHopeGrpcClient<TClient>(
        this IServiceCollection services,
        Uri endpoint)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        var builder = services.AddGrpcClient<TClient>(options => options.Address = endpoint);
        // Local Compose uses HTTP-only internal gRPC endpoints. Permit call
        // credentials there only in Development; production must use TLS.
        if (endpoint.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.ConfigureChannel(options => options.UnsafeUseInsecureChannelCallCredentials = true);
        }
        builder.AddCallCredentials((context, metadata, serviceProvider) =>
            serviceProvider.GetRequiredService<ServiceAuthorizationCallCredentials>().ApplyAsync(context, metadata));
        return builder;
    }

    public static IHttpClientBuilder AddHisHopeGrpcClient<TClient>(
        this IServiceCollection services,
        ServiceEndpointOptions endpoints,
        string endpointKey)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointKey);

        return services.AddHisHopeGrpcClient<TClient>(endpoints.GetRequired(endpointKey));
    }

    public static IHttpClientBuilder AddHisHopeGrpcClient<TClient>(
        this IServiceCollection services,
        IConfiguration configuration,
        string hostName,
        string endpointKey)
        where TClient : class
    {
        var endpoints = RuntimeConfigurationExtensions.BindServiceEndpoints(configuration, hostName);
        return services.AddHisHopeGrpcClient<TClient>(endpoints, endpointKey);
    }
}
