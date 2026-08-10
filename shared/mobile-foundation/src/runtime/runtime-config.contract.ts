export type HisHopeMobileRuntimePlatform = "web" | "android" | "ios";

export interface HisHopeCertificatePinContract {
  readonly host: string;
  readonly sha256Spki: string;
}

export interface HisHopeMobileRuntimeContract {
  readonly apiOrigin: string;
  readonly oidcAuthority: string;
  readonly production?: boolean;
  readonly defaultLocale?: string;
  readonly sentryDsn?: string;
  readonly sentryEnvironment?: string;
  readonly pushNotificationsEnabled?: boolean;
}

export interface HisHopeMobileRuntimePlatformContext {
  readonly isNative: boolean;
  readonly platform: HisHopeMobileRuntimePlatform;
}

export interface HisHopeMobileRuntimeOptions {
  readonly platform: HisHopeMobileRuntimePlatformContext;
  readonly clientId: string;
  readonly scope: string;
  readonly secureRoutes: readonly string[];
  readonly redirectPath: `/${string}`;
  readonly postLogoutRedirectPath: `/${string}`;
  readonly production: boolean;
  readonly appScheme?: string;
  readonly webOrigin?: string;
  readonly appVersion?: string;
  readonly defaultSentryEnvironment?: string;
  readonly defaultPushNotificationsEnabled?: boolean;
  readonly certificatePins?: readonly HisHopeCertificatePinContract[];
}

export interface HisHopeResolvedMobileRuntimeConfig
  extends HisHopeMobileRuntimeContract {
  readonly redirectUrl: string;
  readonly postLogoutRedirectUri: string;
  readonly clientId: string;
  readonly scope: string;
  readonly responseType: "code";
  readonly secureRoutes: string[];
  readonly adminApiUrl: string;
  readonly appVersion: string;
  readonly sentryEnvironment: string;
  readonly pushNotificationsEnabled: boolean;
  readonly certificatePins: readonly HisHopeCertificatePinContract[];
}

declare global {
  interface Window {
    __HISHOPE_CONFIG__?: Partial<HisHopeMobileRuntimeContract>;
  }
}
