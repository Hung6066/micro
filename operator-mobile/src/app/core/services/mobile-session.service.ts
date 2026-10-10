import { Injectable } from "@angular/core";
import { HisHopeMobileSessionService } from "@his-hope/mobile-foundation/angular";

/** Operator-mobile seam over the shared mobile session service. */
@Injectable({ providedIn: "root" })
export class MobileSessionService extends HisHopeMobileSessionService {}
