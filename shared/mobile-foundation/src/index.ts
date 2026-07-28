export type HisHopeMobilePlatform = "web" | "android" | "ios";

export interface HisHopeMobileRuntime {
  readonly platform: HisHopeMobilePlatform;
  readonly isNative: boolean;
}

export interface HisHopeSecureStorage {
  get(key: string): Promise<string | null>;
  set(key: string, value: string): Promise<void>;
  remove(key: string): Promise<void>;
}

export interface HisHopeBiometricCapability {
  isAvailable(): Promise<boolean>;
  authenticate(reason: string): Promise<boolean>;
}

export interface HisHopePushCapability {
  register(): Promise<string | null>;
  unregister(): Promise<void>;
}

export type HisHopeDeviceSecurityStatus =
  | "secure"
  | "compromised"
  | "unsupported"
  | "unknown";

export interface HisHopeDeviceSecurityResult {
  readonly status: HisHopeDeviceSecurityStatus;
  readonly rootedOrJailbroken: boolean;
  readonly emulator: boolean;
  readonly reason?: string;
}

export interface HisHopeCertificatePin {
  readonly host: string;
  /** Base64 SHA-256 SPKI hash, e.g. sha256/.... */
  readonly sha256Spki: string;
}

export interface HisHopeNativeSecurityCapability {
  deviceSecurity(): Promise<HisHopeDeviceSecurityResult>;
  configureCertificatePins(pins: readonly HisHopeCertificatePin[]): Promise<void>;
}

export interface HisHopeNativeHttpRequest {
  readonly url: string;
  readonly method: string;
  readonly headers?: Readonly<Record<string, string>>;
  readonly body?: string;
}

export interface HisHopeNativeHttpResponse {
  readonly status: number;
  readonly headers: Readonly<Record<string, string>>;
  readonly body: string;
}

export interface HisHopeAppPinCapability {
  isConfigured(): Promise<boolean>;
  setPin(pin: string): Promise<void>;
  verifyPin(pin: string): Promise<boolean>;
  clearPin(): Promise<void>;
}

export interface HisHopeCrashReport {
  readonly message: string;
  readonly stack?: string;
  readonly route?: string;
  readonly appVersion: string;
  readonly platform: HisHopeMobilePlatform;
  readonly correlationId?: string;
}

export interface HisHopeRumEvent {
  readonly name: string;
  readonly durationMs?: number;
  readonly route?: string;
  readonly appVersion: string;
  readonly platform: HisHopeMobilePlatform;
  readonly metadata?: Readonly<Record<string, string | number | boolean>>;
}

export interface HisHopeAppPolicy {
  readonly minimumVersion: string;
  readonly latestVersion: string;
  readonly forceUpgrade: boolean;
  readonly storeUrl?: string;
  readonly maintenance: boolean;
}

export interface HisHopeSyncEnvelope {
  readonly idempotencyKey: string;
  readonly operation: string;
  readonly payload: Readonly<Record<string, unknown>>;
  readonly createdAt: string;
}

export interface HisHopeOfflineSyncCapability {
  enqueue(envelope: HisHopeSyncEnvelope): Promise<void>;
  flush(): Promise<{ synced: number; remaining: number }>;
}

export interface HisHopeCameraCaptureOptions {
  quality?: number;
  allowEditing?: boolean;
  width?: number;
  height?: number;
}

export interface HisHopeCameraCapture {
  readonly uri: string;
  readonly format: string;
}

export interface HisHopeCameraCapability {
  capture(
    options?: HisHopeCameraCaptureOptions,
  ): Promise<HisHopeCameraCapture | null>;
}

export interface HisHopeAppLifecycleCapability {
  onResume(listener: () => void): Promise<() => void>;
  onDeepLink(listener: (link: HisHopeDeepLink) => void): Promise<() => void>;
}

/** Adapter boundary for Ionic or a future native pull-to-refresh plugin. */
export interface HisHopeNativeRefreshEvent {
  complete(): Promise<void>;
}

export interface HisHopeNativeRefreshCapability {
  onRefresh(
    listener: (event: HisHopeNativeRefreshEvent) => void,
  ): Promise<() => void>;
}

export interface HisHopeDeepLink {
  readonly path: string;
  readonly query: Readonly<Record<string, string>>;
}

export function createHisHopeMobileRuntime(
  platform: HisHopeMobilePlatform = "web",
): HisHopeMobileRuntime {
  return { platform, isNative: platform === "android" || platform === "ios" };
}

export function parseHisHopeDeepLink(url: string): HisHopeDeepLink | null {
  try {
    const parsed = new URL(url);
    return {
      path: parsed.pathname,
      query: Object.fromEntries(parsed.searchParams.entries()),
    };
  } catch {
    return null;
  }
}

export * from "./secure-storage/his-hope-caching-secure-storage";
export * from "./deep-link/his-hope-deep-link-allow-list";
