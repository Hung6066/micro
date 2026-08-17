# Least-privilege policy for the Harbor certificate issuer.
path "pki_int/sign/harbor-public" {
  capabilities = ["create", "update"]
}

path "pki_int/ca_chain" {
  capabilities = ["read"]
}
