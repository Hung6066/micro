# Identity Service — live integration gates

Chi tiết biến môi trường, Docker/VM/Kubernetes mapping, SIEM/WORM, HA/DR và
FAPI evidence contract nằm tại [identity-external-integrations.vi.md](identity-external-integrations.vi.md).

Run these gates only after the corresponding external tenant, receiver, PKI, or device lab has been approved. The checked-in defaults remain safe: provisioning is `dry-run`, SSF/mTLS/RADIUS are disabled, and device posture is `observe`.

## 1. Baseline

```powershell
pwsh -NoProfile -File .\scripts\config\validate-all-runtimes.ps1
pwsh -NoProfile -File .\scripts\config\validate-identity-live-prerequisites.ps1
pwsh -NoProfile -File .\scripts\config\smoke-public-ui.ps1
```

Do not continue if the foundation/runtime gate fails. `SMOKE_ENVIRONMENT_FLAKY` is a Docker Desktop host-forwarding signal and must be retried; it is not evidence of a vendor connector pass.

## 2. P1 provisioning and federation

### Local Docker smoke

When Windows host port-forwarding is intermittent, validate the application
inside the Compose network before diagnosing an application failure:

```powershell
pwsh -NoProfile -File .\scripts\config\smoke-compose-internal.ps1
```

The gate checks Identity (`identityservice:5003`), gateway, frontend,
dashboard and admin directly on `docker_default`. A passing internal smoke
with a failing `smoke-public-ui.ps1` is classified as Docker Desktop
host-forwarding instability, not an application/container failure.

1. Provision secrets out-of-band (Vault/secret provider) for Google Workspace, Entra ID, or SCIM. Never put secret values in `.env.example`, Compose YAML, or admin-app.
2. Configure the matching `Provisioning__*` endpoints and scopes in the environment-specific secret overlay.
3. Set `PROVISIONING_MODE=dry-run`, deploy, and queue one scoped reconciliation from admin-app.
4. Verify the outbox status, audit correlation id, and no external mutation. Only after vendor contract tests and operator approval set `PROVISIONING_MODE=enabled`.
5. For SSF, configure `SSF_ENABLED=true` and a receiver URL/audience, then verify signed delivery and retry/DLQ behavior before enabling production traffic.

Rollback: set `PROVISIONING_MODE=dry-run` and `SSF_ENABLED=false`, redeploy, and confirm the admin page reports the safe state.

## 3. mTLS and RADIUS EAP-TLS

1. Mount a trusted CA bundle through the VM/Kubernetes secret provider; keep private keys outside the repository.
2. Validate certificate EKU, chain, expiry, and revocation behavior in a non-production client lab.
3. Set `MTLS_ENABLED=true` only for the approved listener and verify thumbprint-only metadata in admin-app.
4. Configure the RADIUS outpost and EAP-TLS trust CA. The Identity Service must never receive or render the RADIUS shared secret.

Rollback: disable `MTLS_ENABLED` and `RADIUS_EAP_TLS_ENABLED`, revoke test bindings, and verify anonymous access remains rejected.

## 4. P2 device posture pilot

1. Connect Chrome Verified Access or Windows device-login lab evidence through the normalized evidence contract; raw attestation and tokens are rejected.
2. Keep `DEVICE_POSTURE_MODE=observe` and use the admin preview endpoint for evidence TTL, replay, provider allow-list, and kill-switch tests.
3. Record provider, policy version, evidence hash prefix, expiry, decision, and correlation id. Do not enforce clinical authorization from this pilot.
4. Promote to `stepup`/`deny` only through a separately approved change and with a tested break-glass path.

Rollback: use the admin kill switch or set `DEVICE_POSTURE_MODE=observe`, then verify a fresh decision remains observable and no clinical request is denied by the pilot.

## 5. Evidence required for completion

The live gate is complete only when the external system returns successful contract evidence, the corresponding audit events exist, and `validate-identity-live-prerequisites.ps1` reports the prerequisite as `READY`. Missing tenant/PKI/lab prerequisites remain `SKIPPED`, never green.

## 6. Current verification snapshot — 2026-08-16

The local adapter/contract layer was re-run after restore with the following
result:

```text
IdentityService.IntegrationTests: Passed 20, Failed 0, Skipped 0
```

The covered local contracts include Google Workspace and Entra provisioning
adapters (fail-closed HTTPS/configuration and token mapping), SSF event
envelope/disabled behavior, mTLS certificate validation, RADIUS EAP-TLS
authorization and certificate requirements, device-posture evidence shaping,
and append-only audit behavior. This is **local contract evidence**, not live
vendor/PKI/device-lab evidence.

The prerequisite validator currently reports all ten external gates as
`LIVE_GATE_SKIPPED`: Google Workspace, Entra ID, SSF receiver, mTLS PKI,
RADIUS EAP-TLS, Chrome Verified Access, Windows device-login lab, SIEM/WORM,
HA/DR and FAPI conformance. The Docker container is intentionally running in
safe defaults (`PROVISIONING_MODE=dry-run`, SSF/mTLS/RADIUS disabled), so
promoting these gates requires real tenant credentials, a mounted CA/PKI,
approved receivers/labs and immutable evidence URIs. Synthetic `.env` values
must not be used to convert these rows to `READY`.
