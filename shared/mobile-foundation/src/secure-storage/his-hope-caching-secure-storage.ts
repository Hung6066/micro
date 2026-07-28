import type { HisHopeSecureStorage } from "../index";

/** Bridges an async secure-storage backend (Keychain/Keystore) with callers that
 *  need synchronous reads — e.g. angular-auth-oidc-client's storage contract.
 *  Call `hydrate()` once (before the first synchronous read) to warm the cache. */
export class HisHopeCachingSecureStorage {
  private readonly cache = new Map<string, string>();
  private hydrated = false;

  constructor(
    private readonly backend: HisHopeSecureStorage,
    private readonly namespaceKey: string,
  ) {}

  async hydrate(): Promise<void> {
    if (this.hydrated) return;
    this.hydrated = true;
    const raw = await this.backend.get(this.namespaceKey);
    if (!raw) return;
    try {
      const entries = JSON.parse(raw) as Record<string, string>;
      Object.entries(entries).forEach(([key, value]) =>
        this.cache.set(key, value),
      );
    } catch {
      // Corrupted blob from a previous app version \u2014 start clean rather than throw.
    }
  }

  read(key: string): string | null {
    return this.cache.get(key) ?? null;
  }

  write(key: string, value: string): void {
    this.cache.set(key, value);
    void this.persist();
  }

  remove(key: string): void {
    this.cache.delete(key);
    void this.persist();
  }

  clear(): void {
    this.cache.clear();
    void this.backend.remove(this.namespaceKey);
  }

  private persist(): Promise<void> {
    const entries: Record<string, string> = {};
    this.cache.forEach((value, key) => {
      entries[key] = value;
    });
    return this.backend.set(this.namespaceKey, JSON.stringify(entries));
  }
}
