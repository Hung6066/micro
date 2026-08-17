import { createAdminRuntimeConfig } from "./app.config";

describe("admin runtime config", () => {
  it("builds redirect urls from the current app origin", () => {
    const config = createAdminRuntimeConfig({
      apiOrigin: "https://gateway.his-hope.test",
      oidcAuthority: "https://identity.his-hope.test",
      production: true,
    });

    expect(config.redirectUrl).toBe(`${window.location.origin}/auth/callback`);
    expect(config.postLogoutRedirectUri).toBe(`${window.location.origin}/auth/login`);
    expect(config.silentRenewUrl).toBe(`${window.location.origin}/auth/silent-refresh`);
  });
});
