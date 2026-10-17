using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Persistence.Tenancy;
using His.Hope.Persistence;
using His.Hope.Infrastructure.Saga;
using His.Hope.ManufacturingService.Infrastructure.Saga;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ManufacturingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddManufacturingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<SoftDeleteInterceptor>();
        services.AddSingleton<ManufacturingAuditSaveChangesInterceptor>();
        services.AddHisHopeTenantAwareNpgsqlDbContextFactory<ManufacturingDbContext>(
            "manufacturing",
            configuration,
            npgsql => npgsql.MigrationsAssembly(typeof(ManufacturingDbContext).Assembly.GetName().Name),
            (sp, builder) => builder.AddInterceptors(
                        sp.GetRequiredService<SoftDeleteInterceptor>(),
                        sp.GetRequiredService<ManufacturingAuditSaveChangesInterceptor>()));
        services.AddSingleton<IManufacturingDbContextFactory>(sp =>
            new ManufacturingDbContextFactoryBridge(sp.GetRequiredService<IHisHopeDbContextFactory<ManufacturingDbContext>>()));
        services.AddSingleton<PostgresManufacturingStore>();
        services.AddSingleton<ManufacturingMobileOperationReplayStore>();
        services.AddSingleton<IManufacturingProductionStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingCapaStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingMaintenanceStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingDashboardStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<ManufacturingTraceabilityReadStore>();
        services.AddSingleton<IManufacturingTraceabilityReadRepository>(sp => sp.GetRequiredService<ManufacturingTraceabilityReadStore>());
        services.AddSingleton<IManufacturingQualityWorkflowStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingRecipeWorkflowStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingComplianceStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<IManufacturingPlanningWorkflowStore>(sp => sp.GetRequiredService<PostgresManufacturingStore>());
        services.AddSingleton<ManufacturingIntegrationStore>();
        services.AddSingleton<IManufacturingIntegrationStore>(sp => sp.GetRequiredService<ManufacturingIntegrationStore>());
        services.AddSingleton<ManufacturingCrossEntityWorkflowStore>();
        services.AddSingleton<IManufacturingWorkflowStore>(sp => sp.GetRequiredService<ManufacturingCrossEntityWorkflowStore>());
        services.AddSingleton<ManufacturingProcurementStore>();
        services.AddSingleton<IManufacturingProcurementStore>(sp => sp.GetRequiredService<ManufacturingProcurementStore>());
        services.AddSingleton<ManufacturingMasterDataStore>();
        services.AddSingleton<IManufacturingMasterDataStore>(sp => sp.GetRequiredService<ManufacturingMasterDataStore>());
        services.AddSingleton<ManufacturingReservationStore>();
        services.AddSingleton<IManufacturingReservationStore>(sp => sp.GetRequiredService<ManufacturingReservationStore>());
        services.AddSagaOptions(configuration);
        services.AddSagaPersistence((sp, options) =>
            options.UseHisHopeNpgsql(
                sp,
                configuration,
                "ManufacturingDb",
                npgsql => npgsql.MigrationsAssembly(typeof(ManufacturingDbContext).Assembly.GetName().Name)));
        services.AddSingleton<ISagaStep<CommerceOrderFulfillmentSagaData>, CommerceOrderFulfillmentSagaStep>();
        services.AddSagaOrchestrator<CommerceOrderFulfillmentSagaData>();
        services.AddSagaRecoveryHandler<CommerceOrderFulfillmentSagaData>();
        services.AddSagaRecoveryService();
        services.AddSingleton<ManufacturingProductionStore>();
        services.AddSingleton<IManufacturingProductionOrderStore>(sp => sp.GetRequiredService<ManufacturingProductionStore>());
        services.AddSingleton<ManufacturingMlDataStore>();
        services.AddSingleton<IManufacturingMlDataStore>(sp => sp.GetRequiredService<ManufacturingMlDataStore>());
        services.AddSingleton<ManufacturingLifecycleAutomation>();
        services.AddHostedService<ManufacturingAnalyticsConsumer>();
        services.AddHostedService<CommerceOrderConsumer>();
        services.AddHostedService<ManufacturingOutboxDispatcher>();
        services.AddHostedService<ManufacturingLifecycleAutomationWorker>();

        return services;
    }

    public static void MigrateManufacturingDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IManufacturingDbContextFactory>();
        foreach (var connectionName in dbFactory.GetRegisteredConnectionNames())
        {
            using var db = dbFactory.CreateDbContextForConnection(connectionName);
            db.Database.Migrate();
        }

        using var sagaDb = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SagaDbContext>>().CreateDbContext();
        sagaDb.Database.Migrate();

        scope.ServiceProvider.GetRequiredService<PostgresManufacturingStore>().Initialize();
        if (scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Manufacturing:SeedDemoData"))
            ManufacturingDemoSeeder.Seed(scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>());
    }
}
