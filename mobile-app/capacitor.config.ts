import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.hishope.mobile',
  appName: 'His.Hope Mobile',
  webDir: 'dist/mobile-app/browser',
  bundledWebRuntime: false,
  server: { androidScheme: 'https' },
};

export default config;
