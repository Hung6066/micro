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

export interface HisHopeDpopKeyStore {
  get(key: string): Promise<string | null>;
  set(key: string, value: string): Promise<void>;
  remove(key: string): Promise<void>;
}

export interface HisHopeDpopProofService {
  createProof(url: string, method: string, accessToken?: string): Promise<string>;
  clear(): Promise<void>;
}

type HisHopeDpopJwk = JsonWebKey & { kty: "EC"; crv: "P-256"; x: string; y: string; d: string };

/** Pure Web Crypto DPoP implementation. Storage is supplied by the platform adapter. */
export class HisHopeWebCryptoDpopProofService implements HisHopeDpopProofService {
  private keyPromise: Promise<{ privateKey: CryptoKey; publicKey: HisHopeDpopJwk }> | null = null;

  constructor(private readonly store: HisHopeDpopKeyStore, private readonly storageKey = "his-hope.dpop-key.v1") {}

  async createProof(url: string, method: string, accessToken?: string): Promise<string> {
    const { privateKey, publicKey } = await this.keyPair();
    const header = { typ: "dpop+jwt", alg: "ES256", jwk: { kty: publicKey.kty, crv: publicKey.crv, x: publicKey.x, y: publicKey.y } };
    const payload: Record<string, unknown> = { htm: method.toUpperCase(), htu: this.targetUri(url), iat: Math.floor(Date.now() / 1000), jti: this.randomId() };
    if (accessToken) payload.ath = this.base64Url(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(accessToken))));
    const encodedHeader = this.encodeJson(header);
    const encodedPayload = this.encodeJson(payload);
    const signingInput = `${encodedHeader}.${encodedPayload}`;
    const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, privateKey, new TextEncoder().encode(signingInput));
    return `${signingInput}.${this.base64Url(new Uint8Array(signature))}`;
  }

  async clear(): Promise<void> {
    this.keyPromise = null;
    await this.store.remove(this.storageKey);
  }

  private keyPair(): Promise<{ privateKey: CryptoKey; publicKey: HisHopeDpopJwk }> {
    this.keyPromise ??= this.loadOrCreate();
    return this.keyPromise;
  }

  private async loadOrCreate(): Promise<{ privateKey: CryptoKey; publicKey: HisHopeDpopJwk }> {
    const stored = await this.store.get(this.storageKey);
    const jwk = stored ? this.parse(stored) : null;
    if (jwk) return { privateKey: await crypto.subtle.importKey("jwk", jwk, { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]), publicKey: jwk };
    const generated = await crypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, ["sign", "verify"]) as CryptoKeyPair;
    const privateJwk = await crypto.subtle.exportKey("jwk", generated.privateKey) as HisHopeDpopJwk;
    await this.store.set(this.storageKey, JSON.stringify(privateJwk));
    return { privateKey: generated.privateKey, publicKey: privateJwk };
  }

  private parse(value: string): HisHopeDpopJwk | null {
    try { const jwk = JSON.parse(value) as Partial<HisHopeDpopJwk>; return jwk.kty === "EC" && jwk.crv === "P-256" && !!jwk.x && !!jwk.y && !!jwk.d ? jwk as HisHopeDpopJwk : null; } catch { return null; }
  }

  private encodeJson(value: unknown): string { return this.base64Url(new TextEncoder().encode(JSON.stringify(value))); }
  private base64Url(value: Uint8Array): string { let binary = ""; value.forEach(byte => binary += String.fromCharCode(byte)); return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, ""); }
  private randomId(): string { const bytes = new Uint8Array(16); crypto.getRandomValues(bytes); return this.base64Url(bytes); }
  private targetUri(url: string): string { const target = new URL(url); target.search = ""; target.hash = ""; return target.toString(); }
}

export interface HisHopeNativePasskeyCapability {
  isSupported(): Promise<boolean>;
  register(options: Readonly<Record<string, unknown>>): Promise<Readonly<Record<string, unknown>>>;
  authenticate(options: Readonly<Record<string, unknown>>): Promise<Readonly<Record<string, unknown>>>;
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
export * from "./internationalization";
