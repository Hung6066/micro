# Hybrid workload identity for His.Hope

His.Hope does not require Kubernetes for workload identity or Vault database
credentials. Kubernetes is one adapter; SPIRE is the common identity plane for
Kubernetes, Docker, VMs and bare metal.

## Runtime modes

| Runtime | `Vault:AuthMethod` | Bootstrap | Production |
|---|---|---|---|
| Kubernetes | `kubernetes` | projected service-account JWT | allowed |
| VM/bare metal | `spiffe-jwt` | SPIRE Agent + node attestation | allowed |
| Docker Compose E2E profile | `spiffe-jwt` | SPIRE Server/Agent + OIDC Discovery Provider | validation only; not a production HA topology |

`approle` and static tokens are rejected by `VaultTokenProvider` in Production.
Production services use short-lived JWT workload identity and Vault database
leases. Each service maps to a separate Vault database role.

## Docker Compose E2E

The real local pipeline is opt-in and does not change the normal Compose
ports:

```powershell
docker compose -f docker/docker-compose.yml -f docker/docker-compose.spiffe.yml `
  --profile spiffe-e2e up -d patientservice
```

The profile starts a persistent-in-volume SPIRE Server, a Docker-attested
SPIRE Agent, the SPIRE OIDC Discovery Provider, the Vault `jwt-spiffe` mount,
one PostgreSQL `vault_manager` management account, seven per-service Vault
database connections/roles, seven per-service JWT fetchers and a
dynamic-credential probe. SPIRE Server does
not serve the OIDC/JWKS endpoints itself; the OIDC Discovery Provider publishes
`/.well-known/openid-configuration` and `/keys`, which Vault uses to validate
JWT-SVID signatures. Each fetcher sidecar writes a short-lived JWT-SVID into
its own named volume; each application service consumes only its own file and
requests its own database role.

The profile validates these gates:

```powershell
docker compose -f docker/docker-compose.yml -f docker/docker-compose.spiffe.yml `
  --profile spiffe-e2e up -d vault-hybrid-e2e-probe postgres-dynamic-credential-probe
docker compose -f docker/docker-compose.yml -f docker/docker-compose.spiffe.yml `
  --profile spiffe-e2e ps -a
```

The expected evidence is `SPIFFE JWT -> Vault JWT -> one leased PostgreSQL
credential: PASS`, `Vault dynamic credential -> PostgreSQL connection: PASS`,
and `patientservice` healthy. The default Compose password values are test
placeholders only; override `POSTGRES_PASSWORD`,
`VAULT_DB_ADMIN_PASSWORD`, and the Vault root bootstrap token out of band.
Never promote this profile's insecure HTTP OIDC endpoint, Docker socket
attestor, root Vault token, or `0644` JWT file mode to production. Production
must use TLS, a real SPIRE node attestor, per-workload file permissions or
direct Workload API access, Vault HA/unseal controls, and a migration/deployer
identity separate from the application role.

## Configuration examples

```json
{
  "Vault": {
    "Address": "https://vault.internal:8200",
    "AuthMethod": "spiffe-jwt",
    "AuthMount": "jwt-spiffe",
    "Role": "patient-service",
    "SpiffeJwtTokenFile": "/run/spire/jwt/vault.jwt",
    "RequireVault": true,
    "AllowStaticToken": false
  },
  "Vault:Database": {
    "Enabled": true,
    "Role": "patient-service-db",
    "MinimumLeaseSeconds": 900
  },
  "Database": {
    "ConnectionLifetimeSeconds": 60,
    "ConnectionIdleLifetimeSeconds": 30
  }
}
```

The application never receives a database admin credential. Vault uses its
own database management account to create and revoke dynamic users. Configure
the Vault database engine with a dedicated management account, then rotate
that account after onboarding.

## SPIRE runtime

Run a SPIRE Server once per trust domain and a SPIRE Agent on each Kubernetes
node, VM or bare-metal host. Register an entry for each service identity and
write a JWT-SVID for the Vault audience to the path configured by
`SpiffeJwtTokenFile`. The VM systemd template and Docker development profile
are documented under `docker/spire/`.

## Lease and pool policy

Vault roles must use a TTL of at least 15 minutes. His.Hope refreshes the lease
when the configured threshold is reached. Npgsql pools use a short connection
lifetime so connections created with an old leased password drain naturally;
applications must not keep a connection open beyond the lease window.

## Failure policy

- Production: Vault unavailable, missing SVID, invalid role, or expired lease
  fails closed.
- Development: AppRole/static fallback may be enabled explicitly.
- No service logs JWTs, Vault tokens, usernames or database passwords.
## Production Compose profile

For a production-shaped self-hosted deployment, use
`docker/docker-compose.identity-production.yml` and the runbook
`docs/security/production-workload-identity-compose.md`. It replaces the E2E
SQLite/join-token/Docker-socket path with PostgreSQL-backed SPIRE Server,
x509pop + Unix attestation, HTTPS OIDC discovery, Vault Raft/TLS and external
Transit/HSM auto-unseal. It intentionally requires operator-injected secret
files and refuses default credentials or Shamir-only Vault startup.
