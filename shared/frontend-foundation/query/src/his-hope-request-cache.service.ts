import { Injectable } from '@angular/core';
import { Observable, defer, shareReplay, tap } from 'rxjs';

interface CacheEntry {
  expiresAt: number;
  request: Observable<unknown>;
}

/**
 * Short-lived in-memory GET cache for request deduplication.
 * It never persists data, tokens, or PHI to browser storage.
 */
@Injectable({ providedIn: 'root' })
export class HisHopeRequestCacheService {
  private readonly entries = new Map<string, CacheEntry>();

  getOrLoad<T>(key: string, loader: () => Observable<T>, ttlMs = 2_000): Observable<T> {
    const now = Date.now();
    const existing = this.entries.get(key);
    if (existing && existing.expiresAt > now) return existing.request as Observable<T>;

    const request = defer(loader).pipe(
      tap({ error: () => this.entries.delete(key) }),
      // A route/table can unsubscribe while it is being replaced by a new
      // query (for example when URL state is restored). Keep the cache
      // subscription tied to real consumers so an orphaned in-flight request
      // cannot leave the next table load waiting on a stale replay stream.
      shareReplay({ bufferSize: 1, refCount: true }),
    );
    this.entries.set(key, { expiresAt: now + Math.max(0, ttlMs), request });
    return request;
  }

  invalidate(prefix: string): void {
    for (const key of this.entries.keys()) {
      if (key.startsWith(prefix)) this.entries.delete(key);
    }
  }

  clear(): void { this.entries.clear(); }
}
