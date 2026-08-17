import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { firstValueFrom } from "rxjs";
import type { HisHopeSyncEnvelope } from "@his-hope/mobile-foundation";
import { NativeCapabilityService } from "./native-capability.service";
import { environment } from "../../environments/environment";

@Injectable({ providedIn: "root" })
export class MobileSyncQueueService {
  private readonly native = inject(NativeCapabilityService);
  private readonly http = inject(HttpClient);
  private readonly key = "his-hope.mobile.sync.queue";
  private readonly endpoint = `${environment.adminApiUrl.replace(/\/admin$/, "")}/mobile/sync`;

  async enqueue(operation: string, payload: Record<string, unknown>, metadata: Partial<Pick<HisHopeSyncEnvelope, 'entityType' | 'entityId' | 'baseVersion' | 'conflictPolicy'>> = {}): Promise<void> {
    if (metadata.entityType?.toLowerCase().startsWith('patient') && metadata.conflictPolicy !== 'reject_on_stale') {
      throw new Error('Patient data offline writes require reject_on_stale conflict policy.');
    }
    const queue = await this.read();
    queue.push({ idempotencyKey: crypto.randomUUID(), operation, payload, createdAt: new Date().toISOString(), schemaVersion: 1, ...metadata });
    await this.native.secureSet(this.key, JSON.stringify(queue.slice(-100)));
  }

  async flush(): Promise<{ synced: number; remaining: number }> {
    const queue = await this.read();
    let synced = 0;
    const remaining: HisHopeSyncEnvelope[] = [];
    for (const envelope of queue) {
      try { await firstValueFrom(this.http.post(this.endpoint, envelope)); synced++; }
      catch { remaining.push(envelope); }
    }
    await this.native.secureSet(this.key, JSON.stringify(remaining));
    return { synced, remaining: remaining.length };
  }

  private async read(): Promise<HisHopeSyncEnvelope[]> {
    const value = await this.native.secureGet(this.key);
    if (!value) return [];
    try { return JSON.parse(value) as HisHopeSyncEnvelope[]; } catch { return []; }
  }
}
