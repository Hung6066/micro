# Identity Service OIDC Runbook

## OIDC Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/.well-known/openid-configuration` | GET | OIDC discovery metadata |
| `/.well-known/jwks` | GET | JWKS public keys |
| `/connect/authorize` | GET/POST | Authorization endpoint |
| `/connect/token` | POST | Token endpoint |
| `/connect/logout` | POST | End session |
| `/connect/introspect` | POST | Token introspection |

## Key Rotation

Development keys are ephemeral RSA-2048 (regenerated on restart).
Production keys are managed via Vault transit engine with 30-day auto-rotation.

## Troubleshooting

### Token introspection returns inactive
1. Check Redis: `redis-cli KEYS "token_blacklist:*"`
2. Verify clock sync between services
3. Check IdentityService gRPC connectivity

### PKCE verification fails
1. Ensure code_challenge is SHA256(code_verifier)
2. Code is single-use (60s TTL in Redis)
3. Check code_verifier is sent in token request

### Vault health check failing
1. Verify Vault pod: `kubectl get pods -l app=vault`
2. Check AppRole: `vault read auth/approle/role/identity-service`
3. Verify transit key: `vault read transit/keys/jwt-signing`
