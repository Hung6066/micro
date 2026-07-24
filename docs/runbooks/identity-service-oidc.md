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

## Production Preflight

Run these checks before promoting an Identity Service OIDC deployment:

1. Confirm Kubernetes is using persistent RS256 signing material, not the local Docker fallback:
   - `kubectl -n his-hope get deploy identity-service -o yaml | grep -E "OpenIddict__Signing__PrivateKeyPath|Jwt__RsaPrivateKeyPath|Vault__Address|Vault__RoleId|Vault__SecretId|Vault__Transit__KeyName"`
   - `kubectl -n his-hope get secret identity-service-vault`
2. Confirm Vault is healthy and the transit key exists:
   - `kubectl -n his-hope get pods -l app=vault`
   - `vault read transit/keys/jwt-signing`
3. Confirm startup is fail-fast:
   - `kubectl -n his-hope describe pod -l app.kubernetes.io/name=identity-service`
   - Verify the `wait-for-vault` init container completes before the app container starts.
   - Verify `/health/startup` fails when Vault transit signing is unavailable.
4. Confirm issuer consistency:
   - `kubectl -n his-hope get configmap identity-service-config -o yaml`
   - `OpenIddict__Issuer` must match the externally trusted issuer, for example `https://identity.his-hope.local`.
5. Confirm service ports:
   - HTTP/OIDC: `identity-service:5003`
   - gRPC introspection: `identity-service:5007`

Local Docker Compose is for development compatibility only. If `docker/docker-compose.yml` exposes ephemeral signing or `Jwt__Key` defaults, do not use that stack as production evidence for persistent RS256 signing.

## OIDC Smoke Test

After deployment or rotation, verify the published contract:

```bash
curl -fsS https://identity.his-hope.local/.well-known/openid-configuration
curl -fsS https://identity.his-hope.local/.well-known/jwks
```

Check the discovery response for:

- `issuer` equals the configured `OpenIddict__Issuer`
- `authorization_endpoint` points to `/connect/authorize`
- `token_endpoint` points to `/connect/token`
- `jwks_uri` points to `/.well-known/jwks`
- `introspection_endpoint` points to `/connect/introspect`

Issue a canary token through Authorization Code + PKCE from the BFF canary route, then validate:

- access token header uses `alg=RS256`
- token header contains the configured production signing `kid`
- refresh token rotation consumes the old token
- reuse of the consumed refresh token revokes the token family

## Vault Transit Rotation

1. Put the identity deployment in heightened monitoring.
2. Rotate the Vault transit key:
   - `vault write -f transit/keys/jwt-signing/rotate`
3. Confirm the key version increments:
   - `vault read transit/keys/jwt-signing`
4. Ensure `OpenIddict__Signing__PrivateKeyPath` points to the updated material if the OpenIddict runtime is currently using mounted RSA keys.
5. Run the OIDC smoke test and issue a new canary token.
6. Confirm JWKS/discovery exposes the active `kid`.
7. Watch these signals for 15 minutes:
   - `VaultUnhealthy`
   - `TokenIssueLatencyHigh`
   - `IntrospectionLatencyHigh`
   - login success rate

Do not delete old Vault transit key versions while unexpired access or refresh tokens may still reference them.

## Troubleshooting

### Token introspection returns inactive
1. Check Redis: `redis-cli KEYS "token_blacklist:*"`
2. Verify clock sync between services
3. Check IdentityService gRPC connectivity

### Token issuance fails after deployment
1. Check startup health: `kubectl -n his-hope get pods -l app.kubernetes.io/name=identity-service`
2. Inspect init container logs: `kubectl -n his-hope logs deploy/identity-service -c wait-for-vault`
3. Verify `Vault__Address` resolves from the Identity Service pod.
4. Verify `identity-service-vault` contains `role_id` and `secret_id`.
5. Verify Vault policy permits `transit/sign/jwt-signing` and `transit/verify/jwt-signing`.

### PKCE verification fails
1. Ensure code_challenge is SHA256(code_verifier)
2. Code is single-use (60s TTL in Redis)
3. Check code_verifier is sent in token request

### Vault health check failing
1. Verify Vault pod: `kubectl get pods -l app=vault`
2. Check AppRole: `vault read auth/approle/role/identity-service`
3. Verify transit key: `vault read transit/keys/jwt-signing`

### Discovery or JWKS does not match production issuer
1. Compare `OpenIddict__Issuer` in `k8s/base/identity-service.yaml` with the ingress hostname.
2. Confirm the BFF callback and redirect URIs use the same issuer host.
3. Recycle pods after changing issuer configuration.
4. Re-run the OIDC smoke test.

## Rollback

Release N keeps legacy `/api/v1/auth/*` endpoints available during migration. If OIDC deployment fails:

1. Stop new BFF OIDC canary traffic.
2. Keep existing sessions alive where possible; do not purge Redis unless token theft is suspected.
3. Roll back the Identity Service deployment image.
4. Confirm `/api/v1/auth/login`, `/api/v1/auth/refresh`, and `/health` recover.
5. File an incident note with Vault health, Identity Service logs, and discovery/JWKS smoke test output.
