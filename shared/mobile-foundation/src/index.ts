export type HisHopeMobilePlatform = 'web' | 'android' | 'ios';

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

export interface HisHopeDeepLink {
  readonly path: string;
  readonly query: Readonly<Record<string, string>>;
}

export function createHisHopeMobileRuntime(platform: HisHopeMobilePlatform = 'web'): HisHopeMobileRuntime {
  return { platform, isNative: platform === 'android' || platform === 'ios' };
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
