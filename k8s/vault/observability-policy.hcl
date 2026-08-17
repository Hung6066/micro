path "secret/data/his-hope/observability/grafana-oidc" {
  capabilities = ["read"]
}

path "secret/data/his-hope/observability/alertmanager" {
  capabilities = ["read"]
}

path "secret/data/his-hope/observability/object-store" {
  capabilities = ["read"]
}

# All observability CSI classes are read-only and scoped below this prefix.
path "secret/data/his-hope/observability/*" {
  capabilities = ["read"]
}
