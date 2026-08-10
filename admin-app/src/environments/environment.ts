import { RuntimeConfigService } from '../../../shared/frontend-foundation/src/lib/runtime/runtime-config.service';
const runtime = new RuntimeConfigService(typeof window === 'undefined' ? { apiOrigin: 'http://localhost:5000', oidcAuthority: 'http://localhost:5000' } : window.__HISHOPE_CONFIG__ ?? { apiOrigin: 'http://localhost:5000', oidcAuthority: 'http://localhost:5000' }).require();
export const environment = {
  production: false,
  adminApiUrl: `${runtime.apiOrigin}/api/v1/admin`,
  authApiUrl: `${runtime.apiOrigin}/api/v1/auth`,
  dashboardBffUrl: `${runtime.apiOrigin}/api/v1/bff/dashboard`,
  oidc: {
    authority: runtime.oidcAuthority,
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
