# GlitchTip and OpenTelemetry for His.Hope Mobile

This stack separates error tracking from distributed tracing:

```text
Mobile WebView -> GlitchTip DSN (JavaScript errors)
Mobile WebView -> Identity /api/v1/mobile/{crash-reports,rum}
Identity and microservices -> OTLP Collector -> Jaeger
```

The mobile API path remains the durable, authenticated platform boundary. The
GlitchTip DSN is only for scrubbed client-side diagnostics; it must not be used
to send access tokens, cookies, patient identifiers, or request bodies.

## Local startup

From the repository root:

```powershell
docker compose -f docker/docker-compose.yml up -d otel-collector glitchtip-postgres glitchtip-valkey glitchtip
```

- GlitchTip: `http://localhost:8000`
- OTLP HTTP intake: `http://localhost:4318/v1/traces`
- OTLP gRPC intake: `localhost:4317`
- Jaeger: `http://localhost:16686`

Create the first GlitchTip organization and mobile project in the GlitchTip
web UI, then copy its DSN into the mobile release configuration. The local
default `consolemail://` backend prints email instead of sending it.

## Mobile configuration

Set `sentryDsn` in `mobile-app/public/runtime-config.js`, which is loaded before
the Angular bundle. The runtime file is preferred for web deployments and is
copied into Capacitor native assets during sync. Keep development empty unless
a local GlitchTip project is intentionally used. `MobileTelemetryService` scrubs
request credentials, cookies, request data, and user identity before sending
an event.

The existing `/api/v1/mobile/crash-reports` and `/api/v1/mobile/rum` endpoints
remain enabled for durable audit and operational reporting. Their HTTP spans
are exported through the Collector, so mobile RUM can be correlated with the
Identity API request without exposing the Collector directly to the app.
Sentry performance spans are also emitted for `MobileTelemetryService.record`,
so RUM appears in the GlitchTip Performance view when tracing is enabled.

## Production requirements

1. Replace all development secrets with secret-manager values:
   `GLITCHTIP_SECRET_KEY`, `GLITCHTIP_DB_PASSWORD`, and `GLITCHTIP_EMAIL_URL`.
2. Pin the GlitchTip and Collector images by digest in the deployment manifest.
3. Put GlitchTip behind TLS and a private ingress; set `GLITCHTIP_DOMAIN` and
   `GLITCHTIP_ALLOWED_HOSTS` to the public HTTPS host.
4. Disable the Collector `debug` exporter after smoke testing and add a durable
   metrics/logs backend before enabling those pipelines in production.
5. Configure GlitchTip data retention, organization membership, and project
   DSN restrictions. Rotate the DSN if it is ever exposed in a public build.
6. Run `npm run validate:mobile-release` only after replacing certificate pin
   and production telemetry placeholders.

## Verification

```powershell
docker compose -f docker/docker-compose.yml config
docker compose -f docker/docker-compose.yml up -d otel-collector glitchtip-postgres glitchtip-valkey glitchtip
curl.exe -i http://localhost:4318/
curl.exe -i http://localhost:8000/
npm --workspace @his-hope/mobile-app run build
```

The OTLP HTTP root is expected to reject a request without a protobuf payload;
use `/v1/traces` with an OTLP client for a full intake check. A GlitchTip HTTP
response confirms that the web service is reachable; DSN ingestion should be
verified with a controlled non-PHI test error in the created project.
