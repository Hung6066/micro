# Production external secrets contract

The production overlay deliberately does not render `postgres-secret` or
`pgbouncer-secret`. The names remain referenced by the PostgreSQL and sidecar
pods, but the objects must be provisioned out-of-band by the self-hosted
secret-management operator before deployment:

- `postgres-secret`: bootstrap password for the PostgreSQL server only.
- `pgbouncer-secret`: runtime pooler authentication material.
- `redis-secret` and `rabbitmq-secret`: cache and broker bootstrap material.
- `unleash-secret`: feature-flag database URL and initial admin token.

Application database credentials are not stored in these secrets. Backend pods
authenticate to Vault with a projected Kubernetes service-account token and
lease database credentials from the per-service Vault database roles. The
production gate intentionally fails if a placeholder secret is rendered.

For a self-hosted cluster, create these objects from an encrypted Vault-backed
deployment job (or an equivalent approved operator), never by committing the
decoded values to Git.
