using His.Hope.AppointmentService.Domain.Repositories;
using His.Hope.AppointmentService.Infrastructure.Persistence;
using His.Hope.AppointmentService.Infrastructure.Persistence.Repositories;
using His.Hope.Infrastructure.Outbox;
using His.Hope.SharedKernel.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace His.Hope.AppointmentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAppointmentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppointmentDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("AppointmentDb"),
                b =>
                {
                    b.MigrationsAssembly(typeof(AppointmentDbContext).Assembly.FullName);
                    b.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
                })
            .AddInterceptors(new OutboxDomainEventInterceptor()));

        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<DomainEventDispatcher>();
        services.AddOutbox<AppointmentDbContext>();

        return services;
    }
}
