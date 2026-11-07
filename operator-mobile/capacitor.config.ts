import type { CapacitorConfig } from "@capacitor/cli";

const buildFlavor = process.env["HIS_HOPE_BUILD_FLAVOR"] ?? "development";
const androidScheme =
  process.env["CAPACITOR_ANDROID_SCHEME"] === "http"
    ? "http"
    : process.env["CAPACITOR_ANDROID_SCHEME"] === "https"
      ? "https"
      : buildFlavor === "production"
        ? "https"
        : "http";

if (buildFlavor === "production" && androidScheme !== "https") {
  throw new Error(
    "Production mobile build flavor requires CAPACITOR_ANDROID_SCHEME=https.",
  );
}

const config: CapacitorConfig = {
  appId: "com.hishope.operator.mobile",
  appName: "His.Hope Operator Mobile",
  webDir: "dist/operator-mobile/browser",
  bundledWebRuntime: false,
  // Default to https; set CAPACITOR_ANDROID_SCHEME=http only for local emulator
  // debugging against a plain-HTTP dev backend.
  server: {
    androidScheme,
  },
};

export default config;
