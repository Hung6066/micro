export const environment = {
  production: true,
  apiUrl: '/api',
  wsUrl: '/ws',
  identityUrl: window.location.origin,
  authApiUrl: '/api/v1/auth',
  oidc: {
    authority: window.location.origin === 'http://localhost:8082' || window.location.origin === 'http://localhost:4201'
      ? 'http://localhost:5000'
      : window.location.origin,
    clientId: 'his-hope-dashboard',
    redirectUrl: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/auth/login',
    silentRenewUrl: window.location.origin + '/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions',
    responseType: 'code',
    secureRoutes: ['/api/'],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
