import { Capacitor } from "@capacitor/core";
import {
  resolveMobileApiOrigin,
  resolveMobileRedirectUri,
  resolveMobileSentryDsn,
  resolveMobileSentryEnvironment,
  resolveProductionApiOrigin,
  rewriteHisHopeNativeOidcUrl,
} from "./mobile-runtime";

describe("mobile runtime", () => {
  beforeEach(() => {
    spyOn(Capacitor, "isNativePlatform").and.returnValue(false);
  });

  it("uses the configured runtime API origin in production", () => {
    window.__HISHOPE_CONFIG__ = {
      apiOrigin: "https://api.example.test",
      oidcAuthority: "https://identity.example.test",
    };
    expect(resolveProductionApiOrigin()).toBe("https://api.example.test");
    delete window.__HISHOPE_CONFIG__;
  });

  it("fails fast when production API origin is missing", () => {
    window.__HISHOPE_CONFIG__ = {
      oidcAuthority: "https://identity.example.test",
    };
    expect(() => resolveProductionApiOrigin()).toThrowError(
      "Mobile runtime config apiOrigin is required.",
    );
    delete window.__HISHOPE_CONFIG__;
  });

  it("keeps browser callbacks on the current origin", () => {
    window.__HISHOPE_CONFIG__ = {
      apiOrigin: "https://api.example.test",
      oidcAuthority: "https://identity.example.test",
    };
    expect(resolveMobileRedirectUri("/auth/callback")).toBe(
      `${window.location.origin}/auth/callback`,
    );
    delete window.__HISHOPE_CONFIG__;
  });

  it("uses the injected development api origin before Angular boot", () => {
    window.__HISHOPE_CONFIG__ = {
      apiOrigin: "https://gateway.example.test",
      oidcAuthority: "https://identity.example.test",
    };
    expect(resolveMobileApiOrigin()).toBe("https://gateway.example.test");
    delete window.__HISHOPE_CONFIG__;
  });

  it("uses runtime GlitchTip configuration when supplied", () => {
    window.__HISHOPE_CONFIG__ = {
      sentryDsn: "  https://public@example.ingest.local/1  ",
      sentryEnvironment: "staging",
    };
    expect(resolveMobileSentryDsn()).toBe("https://public@example.ingest.local/1");
    expect(resolveMobileSentryEnvironment("production")).toBe("staging");
    delete window.__HISHOPE_CONFIG__;
  });

  it("rejects insecure production origins", () => {
    window.__HISHOPE_CONFIG__ = {
      apiOrigin: "http://api.example.test",
      oidcAuthority: "http://identity.example.test",
      production: true,
    };
    expect(() => resolveProductionApiOrigin()).toThrowError(
      "Production mobile runtime config requires HTTPS apiOrigin and oidcAuthority.",
    );
    delete window.__HISHOPE_CONFIG__;
  });

  it("allows HTTP emulator origins when injected runtime is not production", () => {
    window.__HISHOPE_CONFIG__ = {
      apiOrigin: "http://10.0.2.2:5000",
      oidcAuthority: "http://10.0.2.2:5000",
      production: false,
    };
    expect(resolveProductionApiOrigin()).toBe("http://10.0.2.2:5000");
    delete window.__HISHOPE_CONFIG__;
  });

  it("rewrites Docker Identity login URLs onto the public authority", () => {
    expect(
      rewriteHisHopeNativeOidcUrl(
        "http://identityservice:5003/connect/authorize?client_id=his-hope-mobile",
        "http://10.0.2.2:5000",
      ),
    ).toBe("http://10.0.2.2:5000/connect/authorize?client_id=his-hope-mobile");
  });
});
