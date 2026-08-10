# His.Hope production observability contract

This document is the production boundary for Prometheus, Grafana, Loki,
Jaeger and Alertmanager. The file
`k8s/observability/k3s-observability.yaml` remains a single-node/dev profile.
It must not be scaled by changing `replicas` while its `local-path` RWO PVCs
and filesystem backends are still in use.

## Required topology

| Component | Production shape | State requirement |
|---|---|---|
| Prometheus | two independent replicas, external remote-write/object-store or Thanos/Cortex/Mimir | one PVC per replica; never share a Prometheus TSDB directory |
| Alertmanager | three replicas with a stable peer service | `--cluster.peer`/headless service and persistent silences |
| Grafana | two replicas | external PostgreSQL; no SQLite on a shared volume |
| Loki | distributed read/write/backend components, minimum three ingesters | S3-compatible object store and memberlist/consul ring |
| Jaeger | collector/query agents with an external Elasticsearch/OpenSearch or Tempo backend | all-in-one/Badger is dev only |

The production overlay must provide a StorageClass backed by replicated block
storage or RWX storage and an S3-compatible endpoint. MinIO, Ceph RGW and
Harbor object storage are valid self-hosted choices. The endpoint and keys are
not committed to Git.

## Vault contract

The `observability` Vault role is bound to the `grafana`, `alertmanager` and
`observability-storage` service accounts in namespace `monitoring`. The
SecretProviderClass in `k8s/observability/production-secrets.yaml` reads:

```text
secret/data/his-hope/observability/grafana-oidc
secret/data/his-hope/observability/alertmanager
secret/data/his-hope/observability/object-store
```

The policy must be read-only for these exact paths. Do not bind the role to
`default`, do not place a Vault root token in a Deployment, and do not use a
static Kubernetes Secret as the source of truth.

## Grafana OIDC

Create one OIDC client for the actual HTTPS Grafana origin. Its redirect URI
is:

```text
https://grafana.<production-domain>/login/generic_oauth
```

Seed the client id and secret at the Vault path above. Configure Grafana with
`GF_AUTH_GENERIC_OAUTH_*` environment variables from `grafana-oidc` and map
the identity-service `Admin` role to Grafana `Admin`; all authenticated users
must default to `Viewer`.

## Alertmanager receivers

The receiver secret contains the real Slack webhook, PagerDuty routing key,
and SMTP credentials. Alertmanager must be started with
`--config.expand-env` and its config may reference only these environment
variables:

```text
SLACK_WEBHOOK_URL PAGERDUTY_ROUTING_KEY SMTP_HOST SMTP_PORT
SMTP_USERNAME SMTP_PASSWORD SMTP_FROM SMTP_TO
```

Validate each receiver with a non-production test route, then send one
synthetic alert and record the delivery id/status. A placeholder URL, `xxxxx`
routing key, or example SMTP host is not production evidence.

## Linkerd metrics

The dev manifest allows the monitoring Prometheus to scrape Linkerd Viz over
the cluster CIDR. Production must inject Prometheus into Linkerd and use
`production-linkerd-authorization.yaml` (mTLS identity) or scrape the
Linkerd Viz Prometheus federation endpoint. Do not use the unauthenticated
CIDR rule in production. The identity string must match the cluster's actual
Linkerd trust domain; verify it with `linkerd viz stat` and the proxy identity
configuration before apply.

## Sign-off gates

Production sign-off requires all of the following: two replicas survive a pod
failure; data remains queryable after a node failure; an object-store restore
works; Grafana OIDC login works; Slack/PagerDuty/SMTP each receive a synthetic
alert; Vault rotation refreshes mounted secrets; and Linkerd proxy metrics are
`up` without an unauthenticated authorization rule.
