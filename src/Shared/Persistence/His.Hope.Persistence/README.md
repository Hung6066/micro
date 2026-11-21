# His.Hope.Persistence

Use `AddHisHopeMigrationRunner<TDbContext>()` in a dedicated deployment worker or migration job. Web hosts should keep `Persistence:RunMigrationsOnStartup=false`; migrations are explicit and observable rather than hidden in request-serving startup.

## Shared database performance policy

Register `AddHisHopeDatabasePerformance(configuration)` once in each service and configure its DbContext with the service-provider overload of `UseHisHopeNpgsql`. The policy centralizes connection pool limits, command and connection timeouts, transient retry, batch size, safe EF diagnostics, and slow-command telemetry. Slow-command logs contain duration, command type, and parameter count only; SQL text and parameter values are intentionally excluded.

Tenant-aware PostgreSQL services should use `AddHisHopeTenantAwareNpgsqlDbContextFactory<TContext>`; it composes tenant connection resolution with the shared database policy and leaves only provider migrations configuration and service-specific EF interceptors to the caller. The lower-level factory remains available for non-PostgreSQL or exceptional adapters.

Production defaults remain tracking-safe (`TrackAll` unless explicitly configured), disable sensitive-data logging, and use a 500 ms slow-command threshold. Services still own their DbContext model, query shape, indexes, migrations, and tenant isolation.

Supported configuration keys under `Database` include `ConnectionTimeoutSeconds`, `CommandTimeoutSeconds`, `KeepAliveSeconds`, `MinPoolSize`, `MaxPoolSize`, `ConnectionLifetimeSeconds`, `ConnectionIdleLifetimeSeconds`, `MaxRetryCount`, `MaxRetryDelaySeconds`, `MaxBatchSize`, `DefaultQueryTrackingBehavior`, `EnableDetailedErrors`, `EnableSensitiveDataLogging`, and `SlowQueryThresholdMilliseconds`.
