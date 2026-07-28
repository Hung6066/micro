# His.Hope Mobile Deployment

This document describes the supported deployment path for `mobile-app`, the Identity Service mobile APIs, and the native Android/iOS shells.

## 1. Release architecture

The mobile application is an Angular application packaged by Capacitor:

- Web bundle: `mobile-app/dist/mobile-app/browser`
- Android project: `mobile-app/android`
- iOS project: `mobile-app/ios/App`
- OIDC authority and mobile APIs: API Gateway, normally exposed at `https://<api-host>`
- Mobile policy, push registration, telemetry, and sync: Identity Service under `/api/v1/mobile`
- Durable device registrations and telemetry: Identity PostgreSQL database
- Short-lived idempotency and queue coordination: Redis
- Client crash and performance RUM: GlitchTip through the Sentry-compatible DSN
- Server-side trace correlation: OTLP Collector -> Jaeger

Mobile API endpoints:

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /api/v1/mobile/app-policy` | Anonymous | Minimum/latest version and maintenance state |
| `POST /api/v1/mobile/push-tokens` | Bearer required | Register or refresh a device token |
| `POST /api/v1/mobile/crash-reports` | Anonymous | Bounded crash report ingestion |
| `POST /api/v1/mobile/rum` | Anonymous | Bounded RUM event ingestion |
| `POST /api/v1/mobile/sync` | Bearer required | Idempotent offline sync envelope |

The client does not send directly to the OTLP Collector. It sends scrubbed
JavaScript crash events and performance spans to GlitchTip, while the existing
mobile API keeps a durable operational record. The Identity API request span
is exported through the OTLP Collector, allowing RUM/API correlation without
exposing the Collector to a mobile device.

## 2. Environment preparation

Use Node.js, .NET SDK, Docker, and Capacitor CLI versions already pinned by the repository. Install dependencies from the repository root:

```powershell
npm ci
dotnet restore
```

For local development, start infrastructure and Identity/Gateway:

```powershell
docker compose -f docker/docker-compose.yml up -d postgres redis identityservice apigateway
docker compose -f docker/docker-compose.yml ps identityservice apigateway
```

The Android emulator reaches the local Gateway through `http://10.0.2.2:5000`. iOS Simulator uses `http://localhost:5000`. These HTTP addresses are development-only and are enabled by the debug native configuration.

## 3. Backend deployment

Set production values through the deployment secret store, never by committing them to `docker-compose.yml`:

```text
ConnectionStrings__IdentityDb
ConnectionStrings__Redis
Jwt__Issuer
Jwt__Authority
OpenIddict__InternalJwksUri
Mobile__AppPolicy__MinimumVersion
Mobile__AppPolicy__LatestVersion
Mobile__AppPolicy__ForceUpgrade
Mobile__AppPolicy__StoreUrl
Mobile__AppPolicy__Maintenance
GLITCHTIP_DOMAIN
GLITCHTIP_SECRET_KEY
GLITCHTIP_DB_PASSWORD
GLITCHTIP_EMAIL_URL
GLITCHTIP_RETENTION_DAYS
Otlp__Endpoint=http://otel-collector:4317
```

Deploy Identity Service with the normal image pipeline. The service runs the EF migration runner during startup. The mobile release requires these tables to exist:

- `mobile_device_registrations`
- `mobile_telemetry_events`

Verify the migration and endpoints after deployment:

```powershell
docker compose -f docker/docker-compose.yml logs --tail=200 identityservice
Invoke-WebRequest -UseBasicParsing https://<api-host>/api/v1/mobile/app-policy
```

Expected policy response:

```json
{
  "minimumVersion": "1.0.0",
  "latestVersion": "1.0.0",
  "forceUpgrade": false,
  "storeUrl": null,
  "maintenance": false
}
```

For a database migration failure, stop mobile rollout, correct the migration or connection configuration, and redeploy Identity before releasing a client that depends on the new contract.

## 4. Certificate pinning release inputs

Do not ship `api.his-hope.example` or `sha256/REPLACE_IN_RELEASE`. The release pipeline must provide:

```powershell
$env:HISHOPE_API_HOST = "api.example.com"
$env:HISHOPE_API_ORIGIN = "https://api.example.com"
$env:HISHOPE_API_SPKI = "sha256/<base64-spki-sha256>"
npm run prepare:mobile-release
npm run validate:mobile-release
```

`prepare:mobile-release` updates the production Angular environment and generates the Android Network Security Config pin-set. `validate:mobile-release` must pass before any signed artifact is created.

The SPKI hash must be calculated from the public key used by the production certificate, not copied from a development certificate. Keep a backup certificate/key plan and rotate pins before the current certificate expires. A pin mismatch intentionally blocks the API connection; treat it as a release incident, not as a reason to disable pinning.

The iOS project uses the native `URLSessionDelegate` pinning adapter. The archive and challenge path must still be verified on macOS with Xcode and a physical device. Do not consider the iOS artifact production-ready based only on a Windows Capacitor sync.

## 5. Angular build and tests

Build shared packages before the mobile app when local workspace artifacts are used:

```powershell
npm run build:shared
npm run build --workspace @his-hope/mobile-foundation
npm test --workspace @his-hope/mobile-app
npm run build --workspace @his-hope/mobile-app
```

The mobile test command must finish with all browser tests passing. The production Angular build must stay below the budget in `mobile-app/angular.json`.

## 6. Android deployment

### 6.1 Signing

Copy the sample file locally and provision the keystore through CI secrets:

```powershell
Copy-Item mobile-app/android/keystore.properties.sample mobile-app/android/keystore.properties
```

Replace every `CHANGE_ME` value. `keystore.properties` and the keystore must remain outside source control. A release build that reports `keystore.properties not found` is debug-signed and must not be distributed.

### 6.2 Build

Use JDK 21 for the current Android toolchain:

```powershell
$env:JAVA_HOME = "C:\Program Files\Eclipse Adoptium\jdk-21.0.11.10-hotspot"
npx cap sync android
Push-Location mobile-app/android
./gradlew :app:assembleRelease -PversionName=1.0.0 -PversionCode=10000
Pop-Location
```

For an emulator smoke build only:

```powershell
Push-Location mobile-app/android
./gradlew :app:assembleDebug
Pop-Location
```

Install and verify the signed APK/AAB on a clean emulator or physical device. Verify OIDC login, logout, PIN, biometric unlock, push registration, offline queue flush, deep link callback, and force upgrade.

## 7. iOS deployment

iOS deployment must run on macOS:

```bash
cd mobile-app
npm ci
npx cap sync ios
cd ios
pod install --repo-update
open App/App.xcworkspace
```

In Xcode:

1. Select the `App` target and the production team/bundle identifier.
2. Provision App Store signing and push notification entitlements.
3. Confirm `HisHopeSecurityPlugin.swift` is in Compile Sources.
4. Confirm ATS has no arbitrary-load exception.
5. Confirm the production SPKI pin is configured before the first API request.
6. Archive with a Release scheme.
7. Export through the approved App Store/TestFlight profile.

The archive must be tested on a physical iOS device. Simulator-only validation is insufficient for jailbreak detection, secure storage, biometrics, push delivery, and certificate validation.

## 8. Push notification operations

The client registers its provider token after authentication. Identity stores a protected token plus hash in `mobile_device_registrations`. A production push worker/provider integration must:

- select active, non-revoked registrations;
- remove invalid provider tokens;
- retry transient provider failures with bounded backoff;
- record delivery outcome without storing notification payloads containing PHI;
- audit administrative broadcast actions.

Do not put APNs or FCM private keys in the Angular bundle or Capacitor assets.

## 9. Crash, RUM, and offline sync

Crash and RUM payloads are intentionally bounded and must not contain patient
data, access tokens, cookies, or raw request bodies. The mobile telemetry
service applies these controls before sending to GlitchTip:

- redaction;
- route normalization for UUID/numeric identifiers;
- environment and app-version tagging;
- sampled performance spans for RUM;
- GlitchTip retention and project access controls.

Configure the project DSN in `mobile-app/public/runtime-config.js` before a
web deployment or Capacitor sync:

```js
window.__HISHOPE_CONFIG__ = {
  sentryDsn: "http://public-key@glitchtip.example/<project-id>",
  sentryEnvironment: "production"
};
```

The DSN is a public project identifier, not an API credential. Do not put
GlitchTip administrative tokens, database passwords, or SMTP credentials in
this file. Crash events are visible in the GlitchTip Issues view; RUM spans
are visible in Performance when tracing is enabled. Use a controlled non-PHI
test error after every new DSN/project configuration.

Start the local observability stack with:

```powershell
docker compose -f docker/docker-compose.yml up -d otel-collector glitchtip-postgres glitchtip-valkey glitchtip
```

GlitchTip is available at `http://localhost:8000`, OTLP HTTP at
`http://localhost:4318/v1/traces`, and Jaeger at `http://localhost:16686`.

Offline sync envelopes use an idempotency key. The backend must return the duplicate result for a replayed key. Sync failures remain queued on the device and are retried when the network returns.

## 10. Force upgrade and rollback

To force an upgrade:

```text
Mobile__AppPolicy__MinimumVersion=1.1.0
Mobile__AppPolicy__LatestVersion=1.1.0
Mobile__AppPolicy__ForceUpgrade=true
Mobile__AppPolicy__StoreUrl=https://appstore.example/his-hope
```

Rollout order:

1. Deploy backend compatibility changes first.
2. Publish the new mobile artifact to a staged channel.
3. Update `LatestVersion` while `ForceUpgrade=false`.
4. Monitor authentication, API 401/403, crash rate, sync failures, and push delivery.
5. Set `MinimumVersion` and `ForceUpgrade=true` only after the staged rollout is healthy.

Rollback means restoring the previous mobile policy and backend-compatible image. Never roll back the database by deleting applied migrations; use a forward-compatible corrective migration.

## 11. Production release gate

The release is blocked unless every item passes:

- `npm ci` and `dotnet restore` pass.
- `npm run prepare:mobile-release` ran with real production host/SPKI inputs.
- `npm run validate:mobile-release` passes.
- Angular mobile unit tests pass.
- Mobile production build passes its bundle budget.
- Android release is signed with the production keystore.
- iOS archive is signed and tested on a physical device.
- Identity migration is applied in staging.
- `/api/v1/mobile/app-policy` returns the intended version policy.
- Push registration returns `204` for an authenticated device.
- Crash/RUM ingestion returns `204` and produces a durable record.
- A controlled non-PHI crash appears in the configured GlitchTip project.
- A controlled RUM action appears in GlitchTip Performance.
- OTLP Collector accepts `/v1/traces` and forwards backend spans to Jaeger.
- Offline replay is idempotent.
- Root/jailbreak, App PIN, biometric, deep link, logout, and force-upgrade flows are verified.
- SBOM, dependency scan, container scan, and artifact signature checks pass.
- Rollback owner, on-call owner, and certificate rotation owner are recorded.

## 12. Incident contacts and ownership

Record these values in the deployment ticket for every release:

- mobile release version and commit SHA;
- backend image digest;
- database migration ID;
- Android signing key version;
- iOS provisioning profile version;
- certificate expiry and SPKI rotation date;
- push provider credential version;
- on-call engineer and rollback approver.
