using System.Reflection;
using FluentValidation;
using His.Hope.PatientService.Application.Common.Behaviours;
using His.Hope.Validation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.PatientService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPatientApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(His.Hope.Validation.ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        });

        services.AddHisHopeValidation(Assembly.GetExecutingAssembly());
        services.AddAutoMapper(_ => { }, Assembly.GetExecutingAssembly());

        return services;
    }
}
