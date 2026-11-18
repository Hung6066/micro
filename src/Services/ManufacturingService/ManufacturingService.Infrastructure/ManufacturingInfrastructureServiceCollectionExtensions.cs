using His.Hope.ManufacturingService.Application.Ports;
using His.Hope.ManufacturingService.Infrastructure.Persistence;
using His.Hope.Infrastructure.Configuration;
using His.Hope.Infrastructure.DataLifecycle;
using His.Hope.Persistence.Tenancy;
using His.Hope.Persistence;
using His.Hope.Infrastructure.Saga;
using His.Hope.ManufacturingService.Infrastructure.Saga;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    public static async Task MigrateManufacturingDatabaseAsync(
        this IServiceProvider services,
        bool initializeApplicationStores = true,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IManufacturingDbContextFactory>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("His.Hope.ManufacturingService.DatabaseMigrations");
        foreach (var connectionName in dbFactory.GetRegisteredConnectionNames())
        {
            await using var db = dbFactory.CreateDbContextForConnection(connectionName);
            await MigrateWithAdvisoryLockAsync(
                db,
                "His.Hope.ManufacturingService.Migrations",
                "ManufacturingService",
                connectionName,
                logger,
                cancellationToken);
        }

        await using var sagaDb = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<SagaDbContext>>()
            .CreateDbContextAsync(cancellationToken);
        await MigrateWithAdvisoryLockAsync(
            sagaDb,
            "His.Hope.ManufacturingService.Saga.Migrations",
            "ManufacturingSaga",
            "ManufacturingDb",
            logger,
            cancellationToken);

        if (!initializeApplicationStores) return;

        scope.ServiceProvider.GetRequiredService<PostgresManufacturingStore>().Initialize();
        if (scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Manufacturing:SeedDemoData"))
            ManufacturingDemoSeeder.Seed(scope.ServiceProvider.GetRequiredService<IDbContextFactory<ManufacturingDbContext>>());
    }

    private static async Task MigrateWithAdvisoryLockAsync(
        DbContext db,
        string lockNamespace,
        string schemaOwner,
        string connectionName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        var phase = "open_connection";
        var connectionOpened = false;
        var lockAcquired = false;
        logger.LogInformation(
            "Manufacturing database migration started for {SchemaOwner} connection {ConnectionName}",
            schemaOwner,
            connectionName);
        try
        {
            logger.LogInformation(
                "Manufacturing database migration phase started: {Phase} for {SchemaOwner} connection {ConnectionName}",
                phase,
                schemaOwner,
                connectionName);
            elapsed.Restart();
            await db.Database.OpenConnectionAsync(cancellationToken);
            connectionOpened = true;
            logger.LogInformation(
                "Manufacturing database migration phase completed: {Phase} for {SchemaOwner} connection {ConnectionName} after {ElapsedMilliseconds} ms",
                phase,
                schemaOwner,
                connectionName,
                elapsed.ElapsedMilliseconds);

            phase = "acquire_advisory_lock";
            logger.LogInformation(
                "Manufacturing database migration phase started: {Phase} for {SchemaOwner} connection {ConnectionName}",
                phase,
                schemaOwner,
                connectionName);
            elapsed.Restart();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock(hashtext({lockNamespace}), hashtext({connectionName}))",
                cancellationToken);
            lockAcquired = true;
            logger.LogInformation(
                "Manufacturing database migration phase completed: {Phase} for {SchemaOwner} connection {ConnectionName} after {ElapsedMilliseconds} ms",
                phase,
                schemaOwner,
                connectionName,
                elapsed.ElapsedMilliseconds);

            phase = "apply_migrations";
            logger.LogInformation(
                "Manufacturing database migration phase started: {Phase} for {SchemaOwner} connection {ConnectionName}",
                phase,
                schemaOwner,
                connectionName);
            elapsed.Restart();
            await db.Database.MigrateAsync(cancellationToken);
            logger.LogInformation(
                "Manufacturing database migration phase completed: {Phase} for {SchemaOwner} connection {ConnectionName} after {ElapsedMilliseconds} ms",
                phase,
                schemaOwner,
                connectionName,
                elapsed.ElapsedMilliseconds);

            phase = "verify_schema";
            logger.LogInformation(
                "Manufacturing database migration phase started: {Phase} for {SchemaOwner} connection {ConnectionName}",
                phase,
                schemaOwner,
                connectionName);
            elapsed.Restart();
            MigrationSchemaVerifier.Verify(db, schemaOwner);
            logger.LogInformation(
                "Manufacturing database migration phase completed: {Phase} for {SchemaOwner} connection {ConnectionName} after {ElapsedMilliseconds} ms",
                phase,
                schemaOwner,
                connectionName,
                elapsed.ElapsedMilliseconds);
            logger.LogInformation(
                "Manufacturing database migration completed for {SchemaOwner} connection {ConnectionName}",
                schemaOwner,
                connectionName);
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Manufacturing database migration failed in phase {Phase} for {SchemaOwner} connection {ConnectionName} after {ElapsedMilliseconds} ms; exception type {ExceptionType}, HRESULT {HResult}",
                phase,
                schemaOwner,
                connectionName,
                elapsed.ElapsedMilliseconds,
                exception.GetType().Name,
                exception.HResult);
            throw;
        }
        finally
        {
            try
            {
                if (lockAcquired)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_unlock(hashtext({lockNamespace}), hashtext({connectionName}))",
                        CancellationToken.None);
                }
            }
            finally
            {
                if (connectionOpened)
                    await db.Database.CloseConnectionAsync();
            }
        }
    }
}
