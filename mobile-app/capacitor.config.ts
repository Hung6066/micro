import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.hishope.mobile',
  appName: 'His.Hope Mobile',
  webDir: 'dist/mobile-app/browser',
  bundledWebRuntime: false,
  // Local Identity Service runs on HTTP. Set CAPACITOR_ANDROID_SCHEME=https in production CI.
  server: { androidScheme: process.env['CAPACITOR_ANDROID_SCHEME'] === 'https' ? 'https' : 'http' },
};

export default config;
