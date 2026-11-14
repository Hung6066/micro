using His.Hope.Configuration;
using His.Hope.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.ServiceDefaults;

/// <summary>
/// Golden-path registration for outbound HTTP. Callers declare only the client
/// name and operation; resilience, cancellation and optional service
/// authentication are installed at the shared seam.
/// </summary>
public static class HisHopeHttpClientExtensions
{
    public static IHttpClientBuilder AddHisHopeExternalHttpClient(
        this IServiceCollection services,
        string name,
        string operationName,
        Action<HttpClient>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return services.AddHttpClient(name, configure ?? (_ => { }))
            .UseHisHopeOutboundHttp(operationName);
    }

    public static IHttpClientBuilder AddHisHopeExternalHttpClient(
        this IServiceCollection services,
        string name,
        string operationName,
        Action<IServiceProvider, HttpClient> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return services.AddHttpClient(name, configure)
            .UseHisHopeOutboundHttp(operationName);
    }

    public static IHttpClientBuilder AddHisHopeExternalHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        string operationName,
        Action<HttpClient>? configure = null)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return services.AddHttpClient<TClient, TImplementation>(configure ?? (_ => { }))
            .UseHisHopeOutboundHttp(operationName);
    }

    public static IHttpClientBuilder AddHisHopeExternalHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        string operationName,
        Action<IServiceProvider, HttpClient> configure)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return services.AddHttpClient<TClient, TImplementation>(configure)
            .UseHisHopeOutboundHttp(operationName);
    }

    public static IHttpClientBuilder AddHisHopeServiceHttpClient(
        this IServiceCollection services,
        string name,
        string operationName,
        Action<HttpClient>? configure = null)
    {
        return services.AddHisHopeExternalHttpClient(name, operationName, configure)
            .AddHisHopeServiceAuthentication();
    }

    public static IHttpClientBuilder UseHisHopeOutboundHttp(
        this IHttpClientBuilder builder,
        string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        return builder.UseHisHopeResilience(operationName);
    }
}
