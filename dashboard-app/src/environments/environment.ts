export const environment = {
  production: false,
  apiUrl: 'http://localhost:5700/api',
  wsUrl: '/ws',
  identityUrl: 'http://localhost:5001',
  authApiUrl: 'http://localhost:5000/api/v1/auth',
  oidc: {
    // All web applications share the gateway-backed Identity authority. The
    // app origin is only the redirect origin; using it as the authority makes
    // cross-app SSO work on production-like non-default ports fail.
    authority: 'http://localhost:5000',
    clientId: 'his-hope-dashboard',
    redirectUrl: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/auth/login',
    silentRenewUrl: window.location.origin + '/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions offline_access',
    responseType: 'code',
    secureRoutes: ['/api/'],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
