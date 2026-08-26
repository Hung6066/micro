using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using His.Hope.ManufacturingService.Application.Ports;

public static class ManufacturingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddManufacturingInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<ManufacturingDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddSingleton<PostgresManufacturingStore>();
        services.AddSingleton<IManufacturingProductionStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingCapaStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingMaintenanceStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingDashboardStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingLegacyStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<ManufacturingProcurementStore>();
        services.AddSingleton<IManufacturingProcurementStore>(sp => sp.GetRequiredService<ManufacturingProcurementStore>());
        services.AddSingleton<ManufacturingMasterDataStore>();
        services.AddSingleton<IManufacturingMasterDataStore>(sp => sp.GetRequiredService<ManufacturingMasterDataStore>());
        services.AddSingleton<ManufacturingReservationStore>();
        services.AddSingleton<IManufacturingReservationStore>(sp => sp.GetRequiredService<ManufacturingReservationStore>());
        services.AddSingleton<ManufacturingProductionStore>();
        services.AddSingleton<IManufacturingProductionOrderStore>(sp => sp.GetRequiredService<ManufacturingProductionStore>());
        services.AddHostedService<ManufacturingAnalyticsConsumer>();
        services.AddHostedService<CommerceOrderConsumer>();
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
