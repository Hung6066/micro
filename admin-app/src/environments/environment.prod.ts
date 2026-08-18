import { RuntimeConfigService } from "@his-hope/frontend-foundation";

const runtime = new RuntimeConfigService(window.__HISHOPE_CONFIG__).require();

export const environment = {
  production: true,
  adminApiUrl: `${runtime.apiOrigin}/api/v1/admin`,
  authApiUrl: `${runtime.apiOrigin}/api/v1/auth`,
  dashboardBffUrl: `${runtime.apiOrigin}/api/v1/bff/system-dashboard`,
  oidc: {
    authority: runtime.oidcAuthority,
    clientId: "his-hope-admin",
    redirectUrl: `${window.location.origin}/auth/callback`,
    postLogoutRedirectUri: `${window.location.origin}/auth/login`,
    silentRenewUrl: `${window.location.origin}/auth/silent-refresh`,
    scope: "openid profile email roles hishop:permissions hishop:admin",
    responseType: "code",
    secureRoutes: ["/api/v1/admin/"],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
