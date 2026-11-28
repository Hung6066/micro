import { RuntimeConfigService } from "@his-hope/frontend-foundation";

const runtime = new RuntimeConfigService(
  typeof window === "undefined"
    ? { apiOrigin: "http://localhost:5000", oidcAuthority: "http://localhost:5000" }
    : (window.__HISHOPE_CONFIG__ ?? {
        apiOrigin: window.location.origin,
        oidcAuthority: window.location.origin,
      }),
).require();

const appOrigin =
  typeof window === "undefined" ? "http://localhost:4200" : window.location.origin;

export const environment = {
  production: false,
  shellTitle: "Manufacturing Operator Console",
  homeTenantKey: "manufacturing",
  adminApiUrl: `${runtime.apiOrigin}/api/v1/admin`,
  commerceApiUrl: `${runtime.apiOrigin}/api/v1/commerce`,
  contentApiUrl: `${runtime.apiOrigin}/api/v1/content`,
  manufacturingApiUrl: `${runtime.apiOrigin}/api/v1/manufacturing`,
  authApiUrl: `${runtime.apiOrigin}/api/v1/auth`,
  oidc: {
    authority: runtime.oidcAuthority,
    clientId: "manufacturing-app",
    redirectUrl: `${appOrigin}/auth/callback`,
    postLogoutRedirectUri: `${appOrigin}/auth/login`,
    silentRenewUrl: `${appOrigin}/auth/silent-refresh`,
    scope: "openid profile email roles hishop:permissions hishop:admin",
    responseType: "code" as const,
    secureRoutes: [
      "/api/v1/admin/",
      "/api/v1/commerce/",
      "/api/v1/content/",
      "/api/v1/manufacturing/",
    ],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
