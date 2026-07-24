export const environment = {
  production: false,
  apiUrl: '/api/v1',
  useMockServices: false,
  tokenWhitelistedDomains: ['localhost:5000', 'localhost:4200', 'localhost:8081'],

  /** OpenTelemetry Collector HTTP endpoint for trace export. */
  otelCollectorUrl: '',

  oidc: {
    authority: window.location.origin,
    clientId: 'his-hope-spa',
    redirectUrl: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/auth/login',
    silentRenewUrl: window.location.origin + '/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions offline_access',
    responseType: 'code',
    secureRoutes: ['/api/'],
    usePkce: true,
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
