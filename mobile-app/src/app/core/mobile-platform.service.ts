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
  HisHopeNativePasskeyCapability,
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
  openPinnedAuthBrowser(options: { url: string }): Promise<void>;
  isPasskeySupported(): Promise<{ supported: boolean }>;
  createPasskey(options: { requestJson: string }): Promise<{ responseJson: string | Record<string, unknown> }>;
  authenticatePasskey(options: { requestJson: string }): Promise<{ responseJson: string | Record<string, unknown> }>;
}

const Security = registerPlugin<HisHopeSecurityPlugin>("HisHopeSecurity");

@Injectable({ providedIn: "root" })
export class MobilePlatformService implements Partial<HisHopeNativePasskeyCapability> {
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

  openPinnedAuthBrowser(url: string): Promise<void> {
    return Security.openPinnedAuthBrowser({ url });
  }

  async isPasskeySupported(): Promise<boolean> {
    return Capacitor.isNativePlatform() && (await Security.isPasskeySupported()).supported;
  }

  async register(options: Readonly<Record<string, unknown>>): Promise<Readonly<Record<string, unknown>>> {
    const result = await Security.createPasskey({ requestJson: JSON.stringify(options) });
    return this.parsePasskeyResponse(result.responseJson);
  }

  async authenticate(options: Readonly<Record<string, unknown>>): Promise<Readonly<Record<string, unknown>>> {
    const result = await Security.authenticatePasskey({ requestJson: JSON.stringify(options) });
    return this.parsePasskeyResponse(result.responseJson);
  }

  private parsePasskeyResponse(value: string | Record<string, unknown>): Readonly<Record<string, unknown>> {
    if (typeof value !== "string") return value;
    return JSON.parse(value) as Readonly<Record<string, unknown>>;
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
