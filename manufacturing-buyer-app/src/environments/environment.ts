import { RuntimeConfigService } from "@his-hope/frontend-foundation";

const runtime = new RuntimeConfigService(
  typeof window === "undefined"
    ? { apiOrigin: "http://localhost:5000", oidcAuthority: "http://localhost:5000" }
    : (window.__HISHOPE_CONFIG__ ?? {
        apiOrigin: window.location.origin,
        oidcAuthority: window.location.origin,
      }),
).require();

export const environment = {
  production: false,
  commerceApiUrl: `${runtime.apiOrigin}/api/v1/commerce`,
  manufacturingApiUrl: `${runtime.apiOrigin}/api/v1/manufacturing`,
  contentApiUrl: `${runtime.apiOrigin}/api/v1/content`,
  authApiUrl: `${runtime.apiOrigin}/api/v1/auth`,
  oidc: {
    authority: runtime.oidcAuthority,
    clientId: "manufacturing-app",
    redirectUrl: "http://localhost:4205/auth/callback",
    postLogoutRedirectUri: "http://localhost:4205/auth/login",
    silentRenewUrl: "http://localhost:4205/auth/silent-refresh",
    scope: "openid profile email roles hishop:permissions",
    responseType: "code" as const,
    secureRoutes: ["/api/v1/commerce/", "/api/v1/manufacturing/"],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
