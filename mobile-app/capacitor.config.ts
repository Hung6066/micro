import type { CapacitorConfig } from "@capacitor/cli";

const config: CapacitorConfig = {
  appId: "com.hishope.mobile",
  appName: "His.Hope Mobile",
  webDir: "dist/mobile-app/browser",
  bundledWebRuntime: false,
  // Default to https; set CAPACITOR_ANDROID_SCHEME=http only for local emulator
  // debugging against a plain-HTTP dev backend.
  server: {
    androidScheme:
      process.env["CAPACITOR_ANDROID_SCHEME"] === "http" ? "http" : "https",
  },
};

export default config;
