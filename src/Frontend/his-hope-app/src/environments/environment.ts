export const environment = {
  production: false,
  apiUrl: '/api/v1',
  useMockServices: false,
  tokenWhitelistedDomains: ['localhost:5000', 'localhost:4200', 'localhost:8081'],

  /** OpenTelemetry Collector HTTP endpoint for trace export. */
  otelCollectorUrl: '',
};
