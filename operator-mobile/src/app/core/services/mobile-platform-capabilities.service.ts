import { Injectable } from "@angular/core";
import { HisHopeMobilePlatformCapabilitiesService } from "@his-hope/mobile-foundation/angular";

/** Operator-mobile seam over the shared platform capability service. */
@Injectable({ providedIn: "root" })
export class MobilePlatformCapabilitiesService extends HisHopeMobilePlatformCapabilitiesService {}
