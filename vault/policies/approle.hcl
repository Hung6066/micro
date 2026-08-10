# AppRole Policy - Auth Method Configuration
# Defines AppRole parameters for service authentication

path "auth/approle/role/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

path "auth/approle/role/+/role-id" {
  capabilities = ["read"]
}

path "auth/approle/role/+/secret-id" {
  capabilities = ["create", "read", "update"]
}

path "auth/approle/login" {
  capabilities = ["create", "read"]
}
