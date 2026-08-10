# Patient Service - Vault Policy
# Grants read access to patient data secrets and infrastructure credentials

path "secret/data/his-hope/patient-service/*" {
  capabilities = ["read", "list"]
}

path "secret/data/his-hope/database/patientdb" {
  capabilities = ["read"]
}

path "secret/data/his-hope/rabbitmq" {
  capabilities = ["read"]
}

path "secret/data/his-hope/redis" {
  capabilities = ["read"]
}

path "pki/cert/ca" {
  capabilities = ["read"]
}
