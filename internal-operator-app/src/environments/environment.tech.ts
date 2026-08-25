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
  shellTitle: "Tech Vendor Console",
  homeTenantKey: "tech-vendor",
  adminApiUrl: `${runtime.apiOrigin}/api/v1/admin`,
  commerceApiUrl: `${runtime.apiOrigin}/api/v1/commerce`,
  authApiUrl: `${runtime.apiOrigin}/api/v1/auth`,
  oidc: {
    authority: runtime.oidcAuthority,
    clientId: "tech-console",
    redirectUrl: "http://localhost:4201/auth/callback",
    postLogoutRedirectUri: "http://localhost:4201/auth/login",
    silentRenewUrl: "http://localhost:4201/auth/silent-refresh",
    scope: "openid profile email roles hishop:permissions hishop:admin",
    responseType: "code" as const,
    secureRoutes: ["/api/v1/admin/"],
    maxIdTokenIatOffsetInSeconds: 600,
  },
};
