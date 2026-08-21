import {
  HisHopeNativeRefreshAdapter,
  HisHopeOfflineSyncAdapter,
} from "./offline-sync.adapter";
import { HisHopeOfflineSyncQueueService } from "./offline-sync-queue.service";
import {
  HIS_HOPE_NATIVE_REFRESH,
  HIS_HOPE_OFFLINE_SYNC,
} from "../tokens";

/** Registers mobile-foundation capability adapters for DI consumers. */
export function provideHisHopeMobilePlatformAdapters() {
  return [
    HisHopeOfflineSyncQueueService,
    HisHopeOfflineSyncAdapter,
    HisHopeNativeRefreshAdapter,
    {
      provide: HIS_HOPE_OFFLINE_SYNC,
      useExisting: HisHopeOfflineSyncAdapter,
    },
    {
      provide: HIS_HOPE_NATIVE_REFRESH,
      useExisting: HisHopeNativeRefreshAdapter,
    },
  ];
}
