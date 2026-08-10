# Identity security production secrets

The Identity Service does not store production federation, token-encryption, or
push credentials in Git. The production overlay reads them through the Vault
CSI provider `identity-service-secrets` and exposes only the required
configuration keys to the application.

Provision these Vault records before enabling the corresponding features:

| Vault path | Required keys | Application use |
| --- | --- | --- |
| `secret/data/his-hope/identity-service/oidc-encryption` | `private_key` | OpenIddict encryption and resource-service JWE decryption |
| `secret/data/his-hope/identity-service/federation/saml` | `idp_metadata` | SAML IdP metadata URL |
| `secret/data/his-hope/identity-service/federation/ldap` | `server`, `bind_dn`, `bind_password`, `search_base` | LDAPS/AD federation |
| `secret/data/his-hope/identity-service/push/firebase` | `credentials_json` | Firebase service-account JSON |
| `secret/data/his-hope/identity-service/push/apns` | `key_id`, `team_id`, `private_key`, `bundle_id` | APNs provider authentication |

Passkey-first adaptive MFA does not introduce a client secret in the Angular or
mobile bundles. Its operational controls are Redis-backed pending sessions,
WebAuthn challenges, native MFA tickets, deployment-owned passkey RP
configuration, and native domain association. Keep the final flow and current
PASS/UNVERIFIED gate status in
[`docs/security/adaptive-passkey-first-mfa.md`](../security/adaptive-passkey-first-mfa.md).

The production overlay intentionally does not generate Vault TLS secrets from
local files. Provision the externally managed `vault-tls` and
`vault-agent-injector-tls` Kubernetes TLS secrets in namespace `his-hope` before
applying the overlay; the overlay references those names directly.

Infrastructure namespaces such as `linkerd`, `monitoring`, and `vault` are
owned by their respective platform manifests, not by the application overlay.

The Firebase service account and APNs key must belong to the production mobile
application. Rotate them in Vault and restart the deployment; do not place
`google-services.json`, an APNs `.p8` key, or private JWE keys in the
repository.

Before a rollout, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-identity-security-deployment.ps1
kubectl -n his-hope get secret identity-service-security
kubectl -n his-hope rollout status deployment/identity-service
```

The final two commands require a configured production cluster and prove that
the secret object was synchronized and consumed; repository tests alone cannot
prove possession of the real provider credentials.

Every API that validates the encrypted access token must receive
`Jwt__RsaEncryptionPrivateKeyPath` pointing to the mounted `private_key`. Do not
copy the private key into an application image or commit it to a service
configuration file; a deployment is not JWE-ready until this resource-side
mount and a real token-decryption probe have both passed.
