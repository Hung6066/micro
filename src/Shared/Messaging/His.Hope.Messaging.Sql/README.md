# His.Hope.Messaging.Sql

Provider-neutral EF Core durable stores for outbox, inbox and idempotency. Supply the provider-specific `DbContextOptions` in the host. This package owns the adapter model, but not migration scheduling or deployment: the host must provide an explicit migration assembly/SQL artifact and invoke it from the deployment migration job before enabling `Messaging:Sql:Enabled`.
