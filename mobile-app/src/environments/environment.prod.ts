export const environment = {
  production: true,
  adminApiUrl: 'https://api.his-hope.example/api/v1/admin',
  oidc: {
    authority: 'https://identity.his-hope.example',
    clientId: 'his-hope-mobile',
    redirectUrl: 'https://mobile.his-hope.example/auth/callback',
    postLogoutRedirectUri: 'https://mobile.his-hope.example/auth/logout-callback',
    scope: 'openid profile email roles hishop:permissions offline_access',
    secureRoutes: ['/api/v1/'],
  },
};
