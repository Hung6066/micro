# His.Hope.Observability.OpenTelemetry

Configuration-driven OpenTelemetry registration. Prometheus scraping is registered through the same shared package for hosts that expose the metrics endpoint.

## Distributed tracing

Set `OTEL_EXPORTER_OTLP_ENDPOINT` to the project's collector **gRPC** HTTP(S) endpoint (typically port 4317, not the HTTP/protobuf `/v1/traces` endpoint). Legacy `OpenTelemetry:OtlpEndpoint` and `Otlp:Endpoint` remain supported in that precedence order after the standard variable. The first supplied value is authoritative: empty, malformed, non-HTTP(S), credential-bearing, query-bearing or fragment-bearing URLs fail registration instead of falling back to another URL or localhost. Without an endpoint, no OTLP exporter is registered and existing local instrumentation/Prometheus behavior remains available.

For the private project collector, mount the root CA PEM bundle read-only and set:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=https://openship-project-otel.<project-namespace>.svc.cluster.local:4317
OTEL_EXPORTER_OTLP_CERTIFICATE=<mounted-root-ca.pem-path>
ASPNETCORE_ENVIRONMENT=Production
```

`OpenTelemetry:CaCertificatePath` and `Otlp:CaCertificatePath` are legacy CA-file aliases after the standard variable. A supplied CA requires HTTPS and a readable PEM bundle containing a root CA; missing/invalid files fail registration. The exporter validates hostname, validity, server-auth usage and the full chain against that CA only, including bundle/presented intermediates. It never accepts name mismatches or falls back to insecure TLS/another system CA. Private-chain revocation checks are offline (`NoCheck`, matching shared broker/Redis trust); certificate downloads are disabled. Without a CA override, normal OS certificate validation remains in force. If no endpoint is configured, the CA file is not read and no exporter is enabled.

`service.name` defaults to the host's registered service name; `OTEL_SERVICE_NAME` can explicitly override it. `deployment.environment.name` uses `ASPNETCORE_ENVIRONMENT`, then `DOTNET_ENVIRONMENT`, then `Environment`, then the matching resource attribute, or `development` if none is supplied. Existing release SHA/digest attributes are retained.

Optional `OTEL_RESOURCE_ATTRIBUTES` accepts comma-separated `key=value` pairs (percent-encode commas in values). Registered service identity and configured environment/release identity override conflicting attributes. Keys containing `tenant` or `project` are ignored; the collector must remove any other untrusted client attribution and assign the authoritative project/tenant itself. No tenant identity is propagated in baggage.

ASP.NET Core and HttpClient use W3C `traceparent`/`tracestate` propagation. `/health`, `/metrics` and their subpaths are excluded from incoming and outgoing spans (not similarly prefixed application routes). Service activity sources and `His.Hope.MediatR` are subscribed. Explicitly configured tracing uses `AlwaysOnSampler`, even for an unsampled remote parent, so the collector can tail-sample errors, requests slower than or equal to one second, and 10% of the remainder.

Instrumentation records exceptions but does not capture request/response bodies, cookies, authorization headers or SQL statements; no database instrumentation is added. Logger activity tracking includes trace/span IDs, and Serilog hosts use the existing span enricher. Existing application logging policies remain unchanged.

Core APIs (identity, patient, appointment, clinical, lab, billing, pharmacy, manufacturing, commerce, content and FHIR gateway) call `AddHisHopeServicePlatform` → `AddHisHopeEnterpriseInfrastructure` → this registration. `ApiGateway` and `SystemDashboard.Bff` call it directly, without creating a second tracer provider.
