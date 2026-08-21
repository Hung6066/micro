import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { firstValueFrom } from "rxjs";
import type { HisHopeSyncEnvelope } from "@his-hope/mobile-foundation";
import {
  HIS_HOPE_OFFLINE_SYNC_CONFIG,
  HIS_HOPE_SECURE_STORAGE,
} from "../tokens";

/** Secure-storage backed offline sync queue shared by His.Hope mobile apps. */
@Injectable({ providedIn: "root" })
export class HisHopeOfflineSyncQueueService {
  private readonly storage = inject(HIS_HOPE_SECURE_STORAGE);
  private readonly http = inject(HttpClient);
  private readonly config = inject(HIS_HOPE_OFFLINE_SYNC_CONFIG);

  async enqueue(
    operation: string,
    payload: Record<string, unknown>,
    metadata: Partial<
      Pick<
        HisHopeSyncEnvelope,
        "entityType" | "entityId" | "baseVersion" | "conflictPolicy"
      >
    > = {},
  ): Promise<void> {
    if (
      metadata.entityType?.toLowerCase().startsWith("patient") &&
      metadata.conflictPolicy !== "reject_on_stale"
    ) {
      throw new Error(
        "Patient data offline writes require reject_on_stale conflict policy.",
      );
    }
    const queue = await this.read();
    queue.push({
      idempotencyKey: crypto.randomUUID(),
      operation,
      payload,
      createdAt: new Date().toISOString(),
      schemaVersion: 1,
      ...metadata,
    });
    await this.storage.set(
      this.config.storageKey,
      JSON.stringify(queue.slice(-100)),
    );
  }

  async flush(): Promise<{ synced: number; remaining: number }> {
    const queue = await this.read();
    let synced = 0;
    const remaining: HisHopeSyncEnvelope[] = [];
    for (const envelope of queue) {
      try {
        await firstValueFrom(this.http.post(this.config.endpoint, envelope));
        synced++;
      } catch {
        remaining.push(envelope);
      }
    }
    await this.storage.set(this.config.storageKey, JSON.stringify(remaining));
    return { synced, remaining: remaining.length };
  }

  private async read(): Promise<HisHopeSyncEnvelope[]> {
    const value = await this.storage.get(this.config.storageKey);
    if (!value) return [];
    try {
      return JSON.parse(value) as HisHopeSyncEnvelope[];
    } catch {
      return [];
    }
  }
}
