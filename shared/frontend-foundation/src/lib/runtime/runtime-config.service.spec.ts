import { RuntimeConfigService } from "./runtime-config.service";

describe("RuntimeConfigService", () => {
  it("fails when oidc authority is missing", () => {
    const service = new RuntimeConfigService({
      apiOrigin: "https://api.his-hope.test",
    });

    expect(() => service.require()).toThrowError(
      "Runtime config oidcAuthority is required.",
    );
  });

  it("fails when the api origin is not a valid absolute url", () => {
    const service = new RuntimeConfigService({
      apiOrigin: "/api",
      oidcAuthority: "https://identity.his-hope.test",
    });

    expect(() => service.require()).toThrowError(
      "Runtime config apiOrigin must be an absolute URL.",
    );
  });

  it("rejects http origins in production", () => {
    const service = new RuntimeConfigService({
      apiOrigin: "http://api.his-hope.test",
      oidcAuthority: "http://identity.his-hope.test",
      production: true,
    });

    expect(() => service.require()).toThrowError(
      "Production runtime config requires HTTPS apiOrigin and oidcAuthority.",
    );
  });
});
