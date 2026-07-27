import { resolveMobileApiOrigin, resolveMobileRedirectUri } from '../app/core/mobile-runtime';

const apiOrigin = resolveMobileApiOrigin();

export const environment = {
  production: true,
  adminApiUrl: `${apiOrigin}/api/v1/admin`,
  oidc: {
    authority: apiOrigin,
    clientId: 'his-hope-mobile',
    redirectUrl: resolveMobileRedirectUri('/auth/callback'),
    postLogoutRedirectUri: resolveMobileRedirectUri('/auth/logout-callback'),
    scope: 'openid profile email roles hishop:permissions offline_access',
    secureRoutes: ['/api/v1/'],
  },
};
