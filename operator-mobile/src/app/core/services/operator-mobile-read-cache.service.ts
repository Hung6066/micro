import { Injectable, signal } from "@angular/core";
import { Observable, catchError, of, tap, throwError } from "rxjs";

interface CacheEntry<T> {
  value: T;
  cachedAt: number;
}

/** Tenant-scoped, short-lived read cache for field screens. No tokens or writes are persisted. */
@Injectable({ providedIn: "root" })
export class OperatorMobileReadCacheService {
  private readonly entries = new Map<string, CacheEntry<unknown>>();
  readonly stale = signal(false);
  readonly lastReadAt = signal<string | null>(null);

  getOrLoad<T>(key: string, loader: () => Observable<T>, ttlMs = 30_000): Observable<T> {
    const now = Date.now();
    const existing = this.entries.get(key) as CacheEntry<T> | undefined;
    if (existing && now - existing.cachedAt <= ttlMs) {
      this.stale.set(false);
      this.lastReadAt.set(new Date(existing.cachedAt).toISOString());
      return of(existing.value);
    }

    return loader().pipe(
      tap((value) => {
        this.entries.set(key, { value, cachedAt: Date.now() });
        this.stale.set(false);
        this.lastReadAt.set(new Date().toISOString());
      }),
      catchError((error: unknown) => {
        const status = (error as { status?: number } | null)?.status;
        const transient = status === undefined || status === 0 || status === 408 || status === 429 || status >= 500;
        if (!existing || !transient) return throwError(() => error);
        this.stale.set(true);
        this.lastReadAt.set(new Date(existing.cachedAt).toISOString());
        return of(existing.value);
      }),
    );
  }

  invalidateTenant(tenantKey: string | null): void {
    if (!tenantKey) return;
    for (const key of this.entries.keys()) if (key.startsWith(`${tenantKey}:`)) this.entries.delete(key);
  }

  clear(): void {
    this.entries.clear();
    this.stale.set(false);
    this.lastReadAt.set(null);
  }
}
