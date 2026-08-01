export const environment = {
  production: false,
  adminApiUrl: 'http://localhost:5000/api/v1/admin',
  authApiUrl: 'http://localhost:5000/api/v1/auth',
  oidc: {
    authority: 'http://localhost:5000',
    clientId: 'his-hope-admin',
    redirectUrl: 'http://localhost:4202/auth/callback',
    postLogoutRedirectUri: 'http://localhost:4202/auth/login',
    silentRenewUrl: 'http://localhost:4202/auth/silent-refresh',
    scope: 'openid profile email roles hishop:permissions hishop:admin',
    responseType: 'code',
    secureRoutes: ['/api/v1/admin/'],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
