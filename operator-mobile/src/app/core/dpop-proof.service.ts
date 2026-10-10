import { Injectable, inject } from "@angular/core";
import { NativeCapabilityService } from "./native-capability.service";
import { HisHopeWebCryptoDpopProofService } from "@his-hope/mobile-foundation";

@Injectable({ providedIn: "root" })
export class DpopProofService {
  private readonly native = inject(NativeCapabilityService);
  private readonly implementation = new HisHopeWebCryptoDpopProofService({
    get: key => this.native.secureGet(key),
    set: (key, value) => this.native.secureSet(key, value),
    remove: key => this.native.secureRemove(key),
  }, "his-hope.mobile.dpop-key.v1");

  createProof(url: string, method: string, accessToken?: string): Promise<string> { return this.implementation.createProof(url, method, accessToken); }
  clear(): Promise<void> { return this.implementation.clear(); }
}
