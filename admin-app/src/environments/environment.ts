export const environment = {
  production: false,
  adminApiUrl: 'http://localhost:5001/api/v1/admin',
  oidc: {
    authority: 'http://localhost:5000',
    clientId: 'his-hope-admin',
    redirectUrl: 'http://localhost:4202/auth/callback',
    postLogoutRedirectUri: 'http://localhost:4202/auth/login',
    silentRenewUrl: 'http://localhost:4202/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions hishop:admin offline_access',
    responseType: 'code',
    secureRoutes: ['/api/v1/admin/'],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
