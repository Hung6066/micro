export const environment = {
  production: true,
  apiUrl: '/api/v1',
  useMockServices: false,
  tokenWhitelistedDomains: ['his-hope.example.com', 'localhost:8081', 'localhost:8080', 'localhost:5000'],

  /** OpenTelemetry Collector HTTP endpoint for trace export. */
  // Same-origin Traefik route to the monitoring namespace OTLP/HTTP receiver.
  otelCollectorUrl: 'http://otel.his-hope.local/v1/traces',

  oidc: {
    // K3s local hostnames share one stable Identity issuer. The app origin
    // remains the redirect origin, never the OIDC issuer.
    authority: window.location.hostname.endsWith('.his-hope.local')
      ? 'http://identity.his-hope.local'
      : window.location.origin === 'http://localhost:8081' || window.location.origin === 'http://localhost:4200'
        ? 'http://localhost:5000'
        : window.location.origin,
    clientId: 'his-hope-spa',
    redirectUrl: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/auth/login',
    silentRenewUrl: window.location.origin + '/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions',
    responseType: 'code',
    secureRoutes: ['/api/'],
    usePkce: true,
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
