using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class ManufacturingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddManufacturingInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<ManufacturingDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddSingleton<PostgresManufacturingStore>();
        services.AddSingleton<ManufacturingProcurementStore>();
        services.AddSingleton<ManufacturingReservationStore>();
        services.AddSingleton<ManufacturingProductionStore>();
        services.AddHostedService<ManufacturingAnalyticsConsumer>();
        services.AddHostedService<ManufacturingOutboxDispatcher>();

        return services;
    }

    public static void MigrateManufacturingDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>();
        using var db = dbFactory.CreateDbContext();
        db.Database.Migrate();
        scope.ServiceProvider.GetRequiredService<PostgresManufacturingStore>().Initialize();
    }
}
