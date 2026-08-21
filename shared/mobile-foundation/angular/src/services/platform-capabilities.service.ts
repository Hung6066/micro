import { Injectable, inject } from "@angular/core";
import type { HisHopeNativeRefreshEvent } from "@his-hope/mobile-foundation";
import {
  HIS_HOPE_NATIVE_REFRESH,
  HIS_HOPE_OFFLINE_SYNC,
} from "../tokens";

/** Typed access point for mobile-foundation capability adapters. */
@Injectable({ providedIn: "root" })
export class HisHopeMobilePlatformCapabilitiesService {
  private readonly offlineSync = inject(HIS_HOPE_OFFLINE_SYNC);
  private readonly nativeRefresh = inject(HIS_HOPE_NATIVE_REFRESH);

  flushOfflineSync(): Promise<{ synced: number; remaining: number }> {
    return this.offlineSync.flush();
  }

  bindNativeRefresh(
    listener: (event: HisHopeNativeRefreshEvent) => void | Promise<void>,
  ): Promise<() => void> {
    return this.nativeRefresh.onRefresh(listener);
  }
}
