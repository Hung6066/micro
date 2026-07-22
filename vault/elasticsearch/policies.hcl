# Elasticsearch - Vault Policy
# Grants read access to ES credentials for monitoring and logging

path "secret/data/elasticsearch" {
  capabilities = ["read"]
}
