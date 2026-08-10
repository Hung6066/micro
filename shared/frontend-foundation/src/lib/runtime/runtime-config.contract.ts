export interface HisHopeRuntimeConfigContract {
  readonly apiOrigin: string;
  readonly oidcAuthority: string;
  readonly production?: boolean;
  readonly defaultLocale?: string;
}

export interface HisHopeWebRuntimeOptions {
  readonly clientId: string;
  readonly scope: string;
  readonly secureRoutes: readonly string[];
  readonly redirectPath: `/${string}`;
  readonly postLogoutRedirectPath: `/${string}`;
  readonly silentRenewPath?: `/${string}`;
  readonly responseType?: string;
  readonly maxIdTokenIatOffsetInSeconds?: number;
  readonly usePkce?: boolean;
  readonly localizationApiPath?: `/${string}`;
}

export interface HisHopeWebRuntimeConfig extends HisHopeRuntimeConfigContract {
  readonly localizationApiUrl: string;
  readonly redirectUrl: string;
  readonly postLogoutRedirectUri: string;
  readonly silentRenewUrl?: string;
  readonly clientId: string;
  readonly scope: string;
  readonly secureRoutes: string[];
  readonly responseType: string;
  readonly maxIdTokenIatOffsetInSeconds: number;
  readonly usePkce?: boolean;
}

declare global {
  interface Window {
    __HISHOPE_CONFIG__?: Partial<HisHopeRuntimeConfigContract>;
  }
}
