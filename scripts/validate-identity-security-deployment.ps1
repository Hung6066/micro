$ErrorActionPreference = 'Stop'

$deploymentPaths = @(
    'k8s/base/identity-service.yaml',
    'k8s/overlays/prod/identity-security-patch.yaml',
    'k8s/overlays/prod/kustomization.yaml'
)
$providerPath = 'k8s/vault/vault-csi-provider.yaml'

foreach ($path in @($deploymentPaths + $providerPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing identity security deployment artifact: $path"
    }
}

$deployment = ($deploymentPaths | ForEach-Object { Get-Content -Raw -LiteralPath $_ }) -join "`n"
$provider = Get-Content -Raw -LiteralPath $providerPath

$deploymentRequirements = @(
    'mountPath: /mnt/secrets-store',
    'OpenIddict__Signing__PrivateKeyPath',
    'OpenIddict__Encryption__PrivateKeyPath',
    'Saml2__IdPMetadata',
    'Ldap__BindPassword',
    'PushProviders__FirebaseCredentialsJson',
    'PushProviders__ApnsPrivateKey',
    'Jwt__RsaEncryptionPrivateKeyPath',
    'secretName: identity-service-oidc-encryption',
    'mountPath: /mnt/oidc-secrets'
)

$providerRequirements = @(
    'oidc_encryption_private_key',
    'saml_idp_metadata',
    'ldap_bind_password',
    'firebase_credentials_json',
    'apns_private_key',
    'secretName: identity-service-security'
)

foreach ($requirement in $deploymentRequirements) {
    if ($deployment -notmatch [regex]::Escape($requirement)) {
        throw "Identity deployment is missing required security wiring: $requirement"
    }
}

# Production applies the `his-hope-` namePrefix to CSI resources. Accept both
# the source manifest name and the rendered name while still requiring the
# identity-specific SecretProviderClass reference.
if ($deployment -notmatch 'secretProviderClass:\s+(?:his-hope-)?identity-service-secrets') {
    throw 'Identity deployment is missing required security wiring: secretProviderClass: identity-service-secrets'
}

foreach ($requirement in $providerRequirements) {
    if ($provider -notmatch [regex]::Escape($requirement)) {
        throw "Vault CSI provider is missing required identity secret: $requirement"
    }
}

if ($deployment -match 'value:\s*\$\{(FIREBASE|APNS|LDAP|SAML)_') {
    throw 'Identity deployment must consume federation/push secrets through SecretKeyRef, not unresolved placeholders.'
}

Write-Host 'Identity security deployment gate passed: JWE, SAML, LDAP/AD, Firebase, and APNs secret wiring is present.'
