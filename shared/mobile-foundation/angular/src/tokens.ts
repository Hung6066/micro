import { InjectionToken } from "@angular/core";
import type {
  HisHopeAppPinCapability,
  HisHopeBiometricCapability,
  HisHopeMobileAuthActions,
  HisHopeNativeRefreshCapability,
  HisHopeOfflineSyncCapability,
  HisHopeOfflineSyncConfig,
  HisHopeSecureStorage,
} from "@his-hope/mobile-foundation";

export const HIS_HOPE_SECURE_STORAGE =
  new InjectionToken<HisHopeSecureStorage>("HIS_HOPE_SECURE_STORAGE");

export const HIS_HOPE_BIOMETRIC = new InjectionToken<HisHopeBiometricCapability>(
  "HIS_HOPE_BIOMETRIC",
);

export const HIS_HOPE_APP_PIN = new InjectionToken<HisHopeAppPinCapability>(
  "HIS_HOPE_APP_PIN",
);

export const HIS_HOPE_OFFLINE_SYNC =
  new InjectionToken<HisHopeOfflineSyncCapability>("HIS_HOPE_OFFLINE_SYNC");

export const HIS_HOPE_NATIVE_REFRESH =
  new InjectionToken<HisHopeNativeRefreshCapability>("HIS_HOPE_NATIVE_REFRESH");

export const HIS_HOPE_OFFLINE_SYNC_CONFIG =
  new InjectionToken<HisHopeOfflineSyncConfig>("HIS_HOPE_OFFLINE_SYNC_CONFIG");

export const HIS_HOPE_MOBILE_AUTH = new InjectionToken<HisHopeMobileAuthActions>(
  "HIS_HOPE_MOBILE_AUTH",
);

export const HIS_HOPE_TABLE_API_BASE_URL = new InjectionToken<string>(
  "HIS_HOPE_TABLE_API_BASE_URL",
);

/** Prefix for request-cache invalidation after table mutations (default: `admin`). */
export const HIS_HOPE_TABLE_API_CACHE_NAMESPACE = new InjectionToken<string>(
  "HIS_HOPE_TABLE_API_CACHE_NAMESPACE",
  { factory: () => "admin" },
);
