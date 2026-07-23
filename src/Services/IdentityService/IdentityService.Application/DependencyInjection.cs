using System.Reflection;
using FluentValidation;
using His.Hope.IdentityService.Application.OpenIddict;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.IdentityService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<CustomValidateAuthorizationRequest>();
        services.AddScoped<CustomPopulateTokenClaims>();

        return services;
    }
}
