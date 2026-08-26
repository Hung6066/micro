import {
  HIS_HOPE_APP_PIN,
  HIS_HOPE_BIOMETRIC,
  HIS_HOPE_MOBILE_AUTH,
  HIS_HOPE_OFFLINE_SYNC_CONFIG,
  HIS_HOPE_SECURE_STORAGE,
  HIS_HOPE_TABLE_API_BASE_URL,
  provideHisHopeMobilePlatformAdapters,
} from "@his-hope/mobile-foundation/angular";
import { MobileAuthService } from "./auth.service";
import { NativeCapabilityService } from "./native-capability.service";
import { MobilePlatformService } from "./mobile-platform.service";
import { environment } from "../../environments/environment";

/** Wires shared mobile-foundation Angular adapters for the Identity Admin app. */
export function provideMobileFoundation() {
  const syncEndpoint = `${environment.adminApiUrl.replace(/\/admin$/, "")}/mobile/sync`;
  return [
    ...provideHisHopeMobilePlatformAdapters(),
    {
      provide: HIS_HOPE_OFFLINE_SYNC_CONFIG,
      useValue: {
        storageKey: "his-hope.mobile.sync.queue",
        endpoint: syncEndpoint,
      },
    },
    { provide: HIS_HOPE_TABLE_API_BASE_URL, useValue: environment.adminApiUrl },
    { provide: HIS_HOPE_MOBILE_AUTH, useExisting: MobileAuthService },
    {
      provide: HIS_HOPE_SECURE_STORAGE,
      useFactory: (native: NativeCapabilityService) => ({
        get: (key: string) => native.secureGet(key),
        set: (key: string, value: string) => native.secureSet(key, value),
        remove: (key: string) => native.secureRemove(key),
      }),
      deps: [NativeCapabilityService],
    },
    {
      provide: HIS_HOPE_BIOMETRIC,
      useFactory: (native: NativeCapabilityService) => ({
        isAvailable: () => native.biometricAvailable(),
        authenticate: (reason: string) => native.authenticateBiometric(reason),
      }),
      deps: [NativeCapabilityService],
    },
    {
      provide: HIS_HOPE_APP_PIN,
      useFactory: (platform: MobilePlatformService) => ({
        isConfigured: () => platform.isPinConfigured(),
        setPin: (pin: string) => platform.setAppPin(pin),
        verifyPin: (pin: string) => platform.verifyAppPin(pin),
        clearPin: () => platform.clearAppPin(),
      }),
      deps: [MobilePlatformService],
    },
  ];
}
