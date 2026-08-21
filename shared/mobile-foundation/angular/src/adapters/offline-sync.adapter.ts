import { Injectable, inject } from "@angular/core";
import type {
  HisHopeNativeRefreshCapability,
  HisHopeNativeRefreshEvent,
  HisHopeOfflineSyncCapability,
  HisHopeSyncEnvelope,
} from "@his-hope/mobile-foundation";
import { HisHopeOfflineSyncQueueService } from "./offline-sync-queue.service";

/** Binds the offline-sync contract to the shared secure-storage queue. */
@Injectable({ providedIn: "root" })
export class HisHopeOfflineSyncAdapter implements HisHopeOfflineSyncCapability {
  private readonly queue = inject(HisHopeOfflineSyncQueueService);

  enqueue(envelope: HisHopeSyncEnvelope): Promise<void> {
    return this.queue.enqueue(envelope.operation, { ...envelope.payload }, {
      entityType: envelope.entityType,
      entityId: envelope.entityId,
      baseVersion: envelope.baseVersion,
      conflictPolicy: envelope.conflictPolicy,
    });
  }

  flush(): Promise<{ synced: number; remaining: number }> {
    return this.queue.flush();
  }
}

/** No-op native refresh registration until a Capacitor plugin ships. */
@Injectable({ providedIn: "root" })
export class HisHopeNativeRefreshAdapter
  implements HisHopeNativeRefreshCapability
{
  async onRefresh(
    _listener: (event: HisHopeNativeRefreshEvent) => void,
  ): Promise<() => void> {
    return () => undefined;
  }
}
