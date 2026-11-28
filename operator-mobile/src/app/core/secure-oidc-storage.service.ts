import { Injectable, inject } from "@angular/core";
import { AbstractSecurityStorage } from "angular-auth-oidc-client";
import { HisHopeCachingSecureStorage } from "@his-hope/mobile-foundation";
import { NativeCapabilityService } from "./native-capability.service";

const STORAGE_KEY = "his-hope.mobile.oidc-storage.v1";

/**
 * Backs angular-auth-oidc-client's storage contract with Keychain/Keystore
 * instead of the library default (sessionStorage), via the shared caching
 * adapter. `hydrate()` must run to completion before the first `checkAuth()`
 * call so persisted tokens are available on cold start.
 */
@Injectable({ providedIn: "root" })
export class MobileSecureOidcStorage implements AbstractSecurityStorage {
  private readonly native = inject(NativeCapabilityService);
  private readonly storage = new HisHopeCachingSecureStorage(
    {
      get: (key) => this.native.secureGet(key),
      set: (key, value) => this.native.secureSet(key, value),
      remove: (key) => this.native.secureRemove(key),
    },
    STORAGE_KEY,
  );

  hydrate(): Promise<void> {
    return this.storage.hydrate();
  }

  read(key: string): string | null {
    return this.storage.read(key);
  }

  write(key: string, value: string): void {
    this.storage.write(key, value);
  }

  remove(key: string): void {
    this.storage.remove(key);
  }

  clear(): void {
    this.storage.clear();
  }
}
