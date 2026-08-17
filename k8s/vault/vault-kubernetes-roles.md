# His.Hope Vault Kubernetes roles

Configure these roles through an operator-controlled Vault bootstrap job or
Terraform/OpenTofu. Do not put a Vault root token in a Deployment or Git.

```hcl
path "transit/encrypt/jwt-signing" {
  capabilities = ["update"]
}

path "transit/decrypt/jwt-signing" {
  capabilities = ["update"]
}

path "transit/encrypt/mfa-secret" {
  capabilities = ["update"]
}

path "transit/decrypt/mfa-secret" {
  capabilities = ["update"]
}

path "secret/data/his-hope/identity/client-secrets/*" {
  capabilities = ["read", "create", "update", "delete"]
}
```

```bash
vault auth enable kubernetes
vault write auth/kubernetes/config \
  kubernetes_host="https://kubernetes.default.svc.cluster.local:443" \
  token_reviewer_jwt="@/secure/bootstrap/token-reviewer.jwt" \
  kubernetes_ca_cert="@/secure/bootstrap/ca.crt"

vault write auth/kubernetes/role/identity-service \
  bound_service_account_names=identity-service \
  bound_service_account_namespaces=his-hope \
  audience="vault" \
  policies=identity-service \
  ttl=15m \
  max_ttl=1h
```

Database roles used by the shared resolver must be created separately for
each service. The role names are `identity-service-db`, `patient-service-db`,
`clinical-service-db`, `appointment-service-db`, `lab-service-db`,
`billing-service-db`, and `pharmacy-service-db`. Each role must grant only
the matching service account access to `database/creds/<role>` through a
service-specific policy. The database plugin must rotate the generated user
and revoke the lease on expiry.

The projected service-account token used by the application must have the
`vault` audience. If the cluster default token audience is different, add a
projected `serviceAccountToken` volume with `audience: vault` and point
`Vault__JwtTokenFile` at that file. The bootstrap operator must create one
role and policy per service; `default` is forbidden.

## Observability role

The production monitoring stack uses a separate read-only Vault role. Seed the
paths with the operator/bootstrap job, not with a Kubernetes Secret committed
to Git:

```hcl
path "secret/data/his-hope/observability/grafana-oidc" {
  capabilities = ["read"]
}

path "secret/data/his-hope/observability/alertmanager" {
  capabilities = ["read"]
}

path "secret/data/his-hope/observability/object-store" {
  capabilities = ["read"]
}
```

```bash
vault policy write observability k8s/vault/observability-policy.hcl
vault write auth/kubernetes/role/observability \
  bound_service_account_names='grafana,alertmanager,observability-storage' \
  bound_service_account_namespaces=monitoring \
  audience=vault \
  policies=observability \
  ttl=15m \
  max_ttl=1h
```

The policy file is kept beside this document so the bootstrap pipeline can
review it before applying it.
