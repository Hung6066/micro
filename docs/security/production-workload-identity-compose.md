# Production workload identity on Docker Compose

This profile separates the local E2E setup from the production-shaped trust
boundary. It is self-hosted and does not require Kubernetes, but it still
requires an external trust anchor for Vault auto-unseal: Azure Key Vault,
PKCS#11/HSM or a separate Vault Transit root. A single Vault cannot safely
auto-unseal itself.

Set `VAULT_SEAL_TYPE=azurekeyvault` when the Compose hosts are Azure VMs with
the same User-Assigned Managed Identity. Set `VAULT_SEAL_TYPE=transit` only
when using a separate Transit root. Local Windows Docker does not have Azure
Managed Identity and must not pretend to have one. For local-only testing, add
`docker/docker-compose.identity-local-azure.yml`; it injects an Azure Service
Principal client secret through a Docker secret file. Do not use that override
for production Azure VMs.

## Components

- SPIRE Server replicas use the PostgreSQL `spiredb` datastore instead of
  SQLite. Registration data is therefore shared by the server replicas.
- SPIRE Agent uses x509pop node attestation and Unix workload attestation;
  there is no Docker socket and no join-token bootstrap in this profile.
- SPIRE OIDC Discovery Provider publishes the stable issuer
  `https://oidc.his-hope.local` behind an HTTPS proxy.
- Vault uses three-node Raft storage, TLS 1.3 and Transit auto-unseal.
- PostgreSQL has a dedicated SPIRE datastore account and Vault database
  management account plus a separate migration/deployer account. Application
  roles are short-lived and have no DDL.
- Secret values are mounted as Compose secrets from operator-controlled files.

## Provisioning

1. Provision the CA/certificate chain for PostgreSQL, Vault, OIDC and SPIRE
   x509pop outside Git.
2. Provision the SPIRE agent certificate and key for each trusted host. The
   server CA must be pinned in `spire_node_ca.pem`.
3. Provision the external Transit/HSM key and a narrowly scoped unseal token.
4. Copy `docker/production-identity.env.example` to the ignored
   `docker/production-identity.env` and fill in secret-file paths.
5. Validate the secret inventory and Compose model:

   ```powershell
   pwsh -File scripts/validate-production-identity-compose.ps1
   ```

6. Start the infrastructure without the one-shot provisioner:

   ```powershell
   docker compose --env-file docker/production-identity.env \
     -f docker/docker-compose.identity-production.yml up -d --build
   ```

7. Run the one-shot Vault provisioning job with a short-lived operator token,
   then revoke that token immediately:

   ```powershell
   docker compose --env-file docker/production-identity.env \
     -f docker/docker-compose.identity-production.yml --profile provision run --rm vault-production-bootstrap
   ```

## Database migrations

Runtime services must use the Vault dynamic role only. EF migrations and
schema changes must run from a separate deployer job/account. The production
Compose profile must set `Persistence__RunMigrationsOnStartup=false`; grant
DDL only to the migration identity and revoke/rotate it after deployment.

## Rotation and recovery validation

The test token must be limited to reading and revoking the test lease. It is
not a root token:

```powershell
docker compose --env-file docker/production-identity.env \
  -f docker/docker-compose.identity-production.yml --profile validate run --rm vault-rotation-probe

pwsh -File scripts/validate-vault-failover.ps1 \
  -ComposeEnvFile docker/production-identity.env
```

The rotation probe verifies that the first leased database credential connects,
its lease is revoked, a different credential is issued, and the new credential
connects. The failover probe stops Vault leader 1, checks an initialized and
available follower, then starts the leader again.

## Security boundaries

The profile intentionally fails closed when a secret file, certificate, TLS
CA, Transit unseal token or required environment value is missing. Never add a
default root token, password, `tls_skip_verify`, Docker socket mount or Shamir
fallback to make local startup easier.
