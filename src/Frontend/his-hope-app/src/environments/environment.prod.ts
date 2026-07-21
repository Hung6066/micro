export const environment = {
  production: true,
  apiUrl: '/api/v1',
  useMockServices: false,
  tokenWhitelistedDomains: ['his-hope.example.com', 'localhost:8081', 'localhost:8080', 'localhost:5000'],

  /** OpenTelemetry Collector HTTP endpoint for trace export. */
  otelCollectorUrl: 'http://otel-collector:4318/v1/traces',
};
