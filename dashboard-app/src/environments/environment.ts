export const environment = {
  production: false,
  apiUrl: 'http://localhost:5700/api',
  wsUrl: 'http://localhost:5700/ws',
  identityUrl: 'http://localhost:5001',
  oidc: {
    authority: window.location.origin,
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
