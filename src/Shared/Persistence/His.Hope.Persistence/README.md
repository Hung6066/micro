# His.Hope.Persistence

Use `AddHisHopeMigrationRunner<TDbContext>()` in a dedicated deployment worker or migration job. Web hosts should keep `Persistence:RunMigrationsOnStartup=false`; migrations are explicit and observable rather than hidden in request-serving startup.
