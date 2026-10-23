import { TestBed } from "@angular/core/testing";
import {
  HisHopeNativeRefreshAdapter,
  HisHopeOfflineSyncAdapter,
  HisHopeOfflineSyncQueueService,
  HisHopeMobilePlatformCapabilitiesService,
  HIS_HOPE_NATIVE_REFRESH,
  HIS_HOPE_OFFLINE_SYNC,
  HIS_HOPE_OFFLINE_SYNC_CONFIG,
  HIS_HOPE_SECURE_STORAGE,
} from "@his-hope/mobile-foundation/angular";

describe("MobilePlatformCapabilitiesService", () => {
  it("resolves offline sync and native refresh adapters from DI tokens", async () => {
    TestBed.configureTestingModule({
      providers: [
        HisHopeOfflineSyncQueueService,
        HisHopeOfflineSyncAdapter,
        HisHopeNativeRefreshAdapter,
        HisHopeMobilePlatformCapabilitiesService,
        {
          provide: HIS_HOPE_OFFLINE_SYNC,
          useExisting: HisHopeOfflineSyncAdapter,
        },
        {
          provide: HIS_HOPE_NATIVE_REFRESH,
          useExisting: HisHopeNativeRefreshAdapter,
        },
        {
          provide: HIS_HOPE_OFFLINE_SYNC_CONFIG,
          useValue: { storageKey: "test.queue", endpoint: "/mobile/sync" },
        },
        {
          provide: HIS_HOPE_SECURE_STORAGE,
          useValue: {
            get: async () => null,
            set: async () => undefined,
            remove: async () => undefined,
          },
        },
        {
          provide: HisHopeOfflineSyncAdapter,
          useValue: {
            flush: jasmine
              .createSpy("flush")
              .and.resolveTo({ synced: 1, remaining: 0 }),
          },
        },
      ],
    });

    const capabilities = TestBed.inject(HisHopeMobilePlatformCapabilitiesService);
    await expectAsync(capabilities.flushOfflineSync()).toBeResolvedTo({
      synced: 1,
      remaining: 0,
    });
  });
});
