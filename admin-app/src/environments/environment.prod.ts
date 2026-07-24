export const environment = {
  production: true,
  adminApiUrl: '/api/v1/admin',
  oidc: {
    authority: window.location.origin,
    clientId: 'his-hope-admin',
    redirectUrl: window.location.origin + '/auth/callback',
    postLogoutRedirectUri: window.location.origin + '/auth/login',
    silentRenewUrl: window.location.origin + '/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions hishop:admin offline_access',
    responseType: 'code',
    secureRoutes: ['/api/v1/admin/'],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
