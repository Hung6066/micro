export const environment = {
  production: false,
  apiUrl: '/api/v1',
  useMockServices: false,
  tokenWhitelistedDomains: ['localhost:5000', 'localhost:4200', 'localhost:8081'],

  /** OpenTelemetry Collector HTTP endpoint for trace export. */
  otelCollectorUrl: '',

  oidc: {
    // All web applications share the gateway-backed Identity authority. The
    // app origin is only the redirect origin; using it as the authority makes
    // cross-app SSO work on production-like non-default ports fail.
    authority: 'http://localhost:5000',
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
