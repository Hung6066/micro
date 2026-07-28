import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Capacitor, registerPlugin } from "@capacitor/core";
import { firstValueFrom } from "rxjs";
import type {
  HisHopeAppPolicy,
  HisHopeCertificatePin,
  HisHopeDeviceSecurityResult,
  HisHopeNativeHttpRequest,
  HisHopeNativeHttpResponse,
  HisHopeRumEvent,
} from "@his-hope/mobile-foundation";
import { environment } from "../../environments/environment";

interface HisHopeSecurityPlugin {
  deviceSecurity(): Promise<HisHopeDeviceSecurityResult>;
  configureCertificatePins(options: { pins: readonly HisHopeCertificatePin[] }): Promise<void>;
  isPinConfigured(): Promise<{ configured: boolean }>;
  setAppPin(options: { pin: string }): Promise<void>;
  verifyAppPin(options: { pin: string }): Promise<{ valid: boolean }>;
  clearAppPin(): Promise<void>;
  request(options: HisHopeNativeHttpRequest): Promise<HisHopeNativeHttpResponse>;
}

const Security = registerPlugin<HisHopeSecurityPlugin>("HisHopeSecurity");

@Injectable({ providedIn: "root" })
export class MobilePlatformService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl.replace(/\/admin$/, "");
  readonly upgradeRequired = signal(false);
  readonly maintenance = signal(false);
  readonly storeUrl = signal<string | null>(null);

  async deviceSecurity(): Promise<HisHopeDeviceSecurityResult> {
    if (!Capacitor.isNativePlatform()) return { status: "unsupported", rootedOrJailbroken: false, emulator: false };
    return Security.deviceSecurity();
  }

  async configureCertificatePins(): Promise<void> {
    const pins = environment.security.certificatePins;
    if (Capacitor.isNativePlatform() && pins.length > 0) await Security.configureCertificatePins({ pins });
  }

  async isPinConfigured(): Promise<boolean> {
    if (!Capacitor.isNativePlatform()) return false;
    return (await Security.isPinConfigured()).configured;
  }

  async setAppPin(pin: string): Promise<void> {
    await Security.setAppPin({ pin });
  }

  async verifyAppPin(pin: string): Promise<boolean> {
    return (await Security.verifyAppPin({ pin })).valid;
  }

  async clearAppPin(): Promise<void> {
    await Security.clearAppPin();
  }

  nativeRequest(request: HisHopeNativeHttpRequest): Promise<HisHopeNativeHttpResponse> {
    return Security.request(request);
  }

  async appPolicy(): Promise<HisHopeAppPolicy> {
    const policy = await firstValueFrom(this.http.get<HisHopeAppPolicy>(`${this.baseUrl}/mobile/app-policy`));
    this.maintenance.set(policy.maintenance);
    this.storeUrl.set(policy.storeUrl ?? null);
    this.upgradeRequired.set(policy.forceUpgrade && policy.minimumVersion !== environment.appVersion);
    return policy;
  }

  registerPushToken(token: string, platform: "android" | "ios"): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.baseUrl}/mobile/push-tokens`, { token, platform }));
  }

  reportCrash(report: Record<string, unknown>): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.baseUrl}/mobile/crash-reports`, report));
  }

  reportRum(event: HisHopeRumEvent): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.baseUrl}/mobile/rum`, event));
  }
}
