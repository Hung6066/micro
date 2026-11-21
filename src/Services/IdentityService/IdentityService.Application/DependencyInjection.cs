using System.Reflection;
using FluentValidation;
using His.Hope.Authorization;
using His.Hope.IdentityService.Application.Conglomerate;
using His.Hope.IdentityService.Application.OpenIddict;
using His.Hope.IdentityService.Application.OpenShip;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.AddSingleton<IConglomerateTenantRegistry, ConglomerateTenantRegistry>();
        services.AddSingleton<ICrossTenantAccessPolicy>(serviceProvider =>
        {
            var registry = serviceProvider.GetRequiredService<IConglomerateTenantRegistry>();
            if (!registry.IsEnabled)
                return new DefaultDenyCrossTenantAccessPolicy();

            return new ConfigurableCrossTenantAccessPolicy(
                registry.AllowedCrossTenantPairs.Select(pair =>
                    new CrossTenantAllowedPair(
                        pair.Source,
                        pair.Target,
                        pair.Reason,
                        pair.Permissions,
                        pair.TargetClass,
                        pair.OperatorHomeMatch,
                        pair.RequiresJit,
                        pair.MaxDurationMinutes)),
                registry);
        });

        services.AddScoped<CustomValidateAuthorizationRequest>();
        services.AddScoped<CustomPopulateTokenClaims>();
        services.AddScoped<IOpenIddictSubjectRevoker, OpenIddictSubjectRevoker>();

        // OpenShip console access: options are bound by the host; defaults are
        // applied here so an unconfigured host still behaves as documented.
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<OpenShipConsoleOptions>()
            .PostConfigure(options => options.ApplyDefaults());
        services.AddScoped<IOpenShipConsoleAccessResolver, OpenShipConsoleAccessResolver>();

        return services;
    }
}
