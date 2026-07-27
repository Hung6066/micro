# His.Hope.Observability.OpenTelemetry

Configuration-driven OpenTelemetry registration. OTLP export is enabled only when `OpenTelemetry:OtlpEndpoint` or `Otlp:Endpoint` is configured; Prometheus scraping is registered through the same shared package for hosts that expose the metrics endpoint.
