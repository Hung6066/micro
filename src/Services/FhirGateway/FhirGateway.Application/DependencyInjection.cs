using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.Validation;

namespace His.Hope.FhirGateway.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFhirGatewayApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(His.Hope.Validation.ValidationBehavior<,>));
        });

        services.AddHisHopeValidation(Assembly.GetExecutingAssembly());

        return services;
    }
}
