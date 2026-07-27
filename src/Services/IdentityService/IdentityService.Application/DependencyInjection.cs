using System.Reflection;
using FluentValidation;
using His.Hope.IdentityService.Application.OpenIddict;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.Validation;

namespace His.Hope.IdentityService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(His.Hope.Validation.ValidationBehavior<,>));
        });

        services.AddHisHopeValidation(Assembly.GetExecutingAssembly());

        services.AddScoped<CustomValidateAuthorizationRequest>();
        services.AddScoped<CustomPopulateTokenClaims>();

        return services;
    }
}
