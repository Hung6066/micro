import { Capacitor } from "@capacitor/core";
import {
  resolveMobileRedirectUri,
  resolveMobileSentryDsn,
  resolveMobileSentryEnvironment,
  resolveProductionApiOrigin,
} from "./mobile-runtime";

describe("mobile runtime", () => {
  it("uses the configured runtime API origin in production", () => {
    window.__HISHOPE_CONFIG__ = { apiOrigin: "https://api.example.test" };
    expect(resolveProductionApiOrigin()).toBe("https://api.example.test");
    delete window.__HISHOPE_CONFIG__;
  });

  it("fails fast when production API origin is missing", () => {
    delete window.__HISHOPE_CONFIG__;
    expect(() => resolveProductionApiOrigin()).toThrowError(
      "Production mobile API origin is not configured. Provide window.__HISHOPE_CONFIG__.apiOrigin before boot.",
    );
  });

  it("keeps browser callbacks on the current origin", () => {
    spyOn(Capacitor, "isNativePlatform").and.returnValue(false);
    expect(resolveMobileRedirectUri("/auth/callback")).toBe(
      `${window.location.origin}/auth/callback`,
    );
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
});
