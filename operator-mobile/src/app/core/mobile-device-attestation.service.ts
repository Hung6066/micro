import { Injectable, inject } from "@angular/core";
import { Capacitor } from "@capacitor/core";
import { firstValueFrom } from "rxjs";
import { filter, take } from "rxjs/operators";
import type { HisHopeDeviceAttestationResult, HisHopeDeviceSecurityResult } from "@his-hope/mobile-foundation";
import { MobileAuthService } from "./auth.service";
import { NativeCapabilityService } from "./native-capability.service";
import { MobilePlatformService } from "./mobile-platform.service";

const DEVICE_ID_STORAGE_KEY = "his-hope.mobile.device-id.v1";
const ATTESTATION_COOLDOWN_MS = 15 * 60 * 1000;
const LAST_ATTESTATION_KEY = "his-hope.mobile.attestation-at.v1";

export interface MobileAttestationSubmission {
  readonly userId: string;
  readonly deviceId: string;
  readonly provider: "play-integrity" | "app-attest";
  readonly signals: Readonly<Record<string, boolean>>;
  readonly observedAt: string;
  readonly replayNonce: string;
}

@Injectable({ providedIn: "root" })
export class MobileDeviceAttestationService {
  private readonly auth = inject(MobileAuthService);
  private readonly native = inject(NativeCapabilityService);
  private readonly platform = inject(MobilePlatformService);
  private inFlight: Promise<void> | null = null;

  submitIfEligible(security?: HisHopeDeviceSecurityResult): Promise<void> {
    this.inFlight ??= this.submitInternal(security).finally(() => {
      this.inFlight = null;
    });
    return this.inFlight;
  }

  buildSubmission(
    attestation: HisHopeDeviceAttestationResult,
    userId: string,
    deviceId: string,
    replayNonce: string,
  ): MobileAttestationSubmission {
    return {
      userId,
      deviceId,
      provider: attestation.provider,
      signals: attestation.signals,
      observedAt: new Date().toISOString(),
      replayNonce,
    };
  }

  private async submitInternal(security?: HisHopeDeviceSecurityResult): Promise<void> {
    if (!Capacitor.isNativePlatform()) return;

    const isAuthenticated = await firstValueFrom(this.auth.isAuthenticated$.pipe(take(1)));
    if (!isAuthenticated) return;

    if (await this.isWithinCooldown()) return;

    const userId = await this.resolveUserId();
    if (!userId) return;

    const deviceSecurity = security ?? await this.platform.deviceSecurity();
    if (deviceSecurity.status === "compromised" || deviceSecurity.rootedOrJailbroken) return;

    const attestation = await this.platform.deviceAttestation();
    const deviceId = await this.resolveDeviceId();
    const replayNonce = crypto.randomUUID();
    const payload = this.buildSubmission(attestation, userId, deviceId, replayNonce);

    await this.platform.submitDeviceAttestation({
      userId: payload.userId,
      deviceId: payload.deviceId,
      provider: payload.provider,
      signals: payload.signals,
      observedAt: payload.observedAt,
      replayNonce: payload.replayNonce,
    });

    await this.native.secureSet(LAST_ATTESTATION_KEY, String(Date.now()));
  }

  private async resolveUserId(): Promise<string | null> {
    const result = await firstValueFrom(
      this.auth.userData$.pipe(
        filter(data => !!data?.userData && typeof data.userData["sub"] === "string"),
        take(1),
      ),
    );
    return result.userData["sub"] as string;
  }

  private async resolveDeviceId(): Promise<string> {
    const existing = await this.native.secureGet(DEVICE_ID_STORAGE_KEY);
    if (existing) return existing;
    const deviceId = crypto.randomUUID();
    await this.native.secureSet(DEVICE_ID_STORAGE_KEY, deviceId);
    return deviceId;
  }

  private async isWithinCooldown(): Promise<boolean> {
    const last = await this.native.secureGet(LAST_ATTESTATION_KEY);
    if (!last) return false;
    const elapsed = Date.now() - Number.parseInt(last, 10);
    return Number.isFinite(elapsed) && elapsed >= 0 && elapsed < ATTESTATION_COOLDOWN_MS;
  }
}
