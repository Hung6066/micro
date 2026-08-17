import test from "node:test";
import assert from "node:assert/strict";
import { RuntimeConfigService } from "./runtime-config.service";

test("mobile runtime config fails when oidc authority is missing", () => {
  const service = new RuntimeConfigService({
    apiOrigin: "https://api.his-hope.test",
  });

  assert.throws(
    () =>
      service.require({
        platform: { isNative: true, platform: "android" },
        clientId: "his-hope-mobile",
        scope: "openid",
        secureRoutes: ["/api/v1/"],
        redirectPath: "/auth/callback",
        postLogoutRedirectPath: "/auth/logout-callback",
        production: false,
      }),
    /Mobile runtime config oidcAuthority is required\./,
  );
});

test("mobile runtime config rejects invalid api origins", () => {
  const service = new RuntimeConfigService({
    apiOrigin: "/api",
    oidcAuthority: "https://identity.his-hope.test",
  });

  assert.throws(
    () =>
      service.require({
        platform: { isNative: false, platform: "web" },
        clientId: "his-hope-mobile",
        scope: "openid",
        secureRoutes: ["/api/v1/"],
        redirectPath: "/auth/callback",
        postLogoutRedirectPath: "/auth/logout-callback",
        production: false,
      }),
    /Mobile runtime config apiOrigin must be an absolute URL\./,
  );
});

test("mobile runtime config rejects http origins in production", () => {
  const service = new RuntimeConfigService({
    apiOrigin: "http://api.his-hope.test",
    oidcAuthority: "http://identity.his-hope.test",
  });

  assert.throws(
    () =>
      service.require({
        platform: { isNative: true, platform: "ios" },
        clientId: "his-hope-mobile",
        scope: "openid",
        secureRoutes: ["/api/v1/"],
        redirectPath: "/auth/callback",
        postLogoutRedirectPath: "/auth/logout-callback",
        production: true,
      }),
    /Production mobile runtime config requires HTTPS apiOrigin and oidcAuthority\./,
  );
});

test("mobile runtime config creates native callback uris", () => {
  const service = new RuntimeConfigService({
    apiOrigin: "https://api.his-hope.test",
    oidcAuthority: "https://identity.his-hope.test",
  });

  const config = service.require({
    platform: { isNative: true, platform: "android" },
    clientId: "his-hope-mobile",
    scope: "openid",
    secureRoutes: ["/api/v1/"],
    redirectPath: "/auth/callback",
    postLogoutRedirectPath: "/auth/logout-callback",
    production: false,
  });

  assert.equal(config.redirectUrl, "hishope://auth/callback");
  assert.equal(
    config.postLogoutRedirectUri,
    "hishope://auth/logout-callback",
  );
});
