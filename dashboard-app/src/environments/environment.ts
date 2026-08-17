import { RuntimeConfigService } from '@his-hope/frontend-foundation';
const runtime = new RuntimeConfigService(typeof window === 'undefined' ? { apiOrigin: 'http://localhost:5000', oidcAuthority: 'http://localhost:5000' } : window.__HISHOPE_CONFIG__ ?? { apiOrigin: 'http://localhost:5000', oidcAuthority: 'http://localhost:5000' }).require();
export const environment = {
  production: false,
  apiUrl: `${runtime.apiOrigin}/api`,
  wsUrl: '/ws',
  identityUrl: runtime.oidcAuthority,
  authApiUrl: `${runtime.apiOrigin}/api/v1/auth`,
  oidc: {
    // All web applications share the gateway-backed Identity authority. The
    // app origin is only the redirect origin; using it as the authority makes
    // cross-app SSO work on production-like non-default ports fail.
    authority: runtime.oidcAuthority,
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
