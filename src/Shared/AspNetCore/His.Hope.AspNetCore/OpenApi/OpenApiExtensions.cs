using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.AspNetCore.OpenApi;

public static class OpenApiExtensions
{
    public static IServiceCollection AddHisHopeOpenApi(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        return services;
    }

    public static RouteHandlerBuilder WithHisHopeOpenApi(this RouteHandlerBuilder builder) =>
        builder.WithOpenApi();
}
