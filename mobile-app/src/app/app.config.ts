import {
  ApplicationConfig,
  ErrorHandler,
  inject,
  PLATFORM_ID,
  provideAppInitializer,
} from "@angular/core";
import { DOCUMENT } from "@angular/common";
import { provideAnimations } from "@angular/platform-browser/animations";
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from "@angular/common/http";
import { provideRouter } from "@angular/router";
import { AbstractSecurityStorage, provideAuth } from "angular-auth-oidc-client";
import {
  hisHopeCorrelationIdInterceptor,
  hisHopeErrorInterceptor,
} from "@his-hope/frontend-foundation";
import {
  HisHopeI18nService,
  HisHopeLocalizationApiService,
  hisHopeInternationalizationInterceptor,
  HIS_HOPE_LOCALIZATION_API_URL,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeGlobalErrorHandler } from "@his-hope/frontend-foundation";
import { routes } from "./app.routes";
import { authInterceptor } from "./core/auth.interceptor";
import { mobileNativeHttpInterceptor } from "./core/mobile-native-http.interceptor";
import { mobileSessionInterceptor } from "./core/mobile-session.interceptor";
import { MobileSecureOidcStorage } from "./core/secure-oidc-storage.service";
import { provideMobileFoundation } from "./core/mobile-foundation.providers";
import { MobilePlatformService } from "./core/mobile-platform.service";
import { environment } from "../environments/environment";

export const appConfig: ApplicationConfig = {
  providers: [
    ...provideMobileFoundation(),
    {
      provide: HisHopeI18nService,
      useFactory: (document: Document, platformId: object) =>
        new HisHopeI18nService(document, platformId),
      deps: [DOCUMENT, PLATFORM_ID],
    },
    {
      provide: HisHopeLocalizationApiService,
      useFactory: (
        http: HttpClient,
        i18n: HisHopeI18nService,
        apiUrl: string,
      ) => new HisHopeLocalizationApiService(http, i18n, apiUrl),
      deps: [HttpClient, HisHopeI18nService, HIS_HOPE_LOCALIZATION_API_URL],
    },
    {
      provide: HIS_HOPE_LOCALIZATION_API_URL,
      useValue: `${environment.oidc.authority}/api/v1`,
    },
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(
      withInterceptors([
        hisHopeCorrelationIdInterceptor,
        hisHopeInternationalizationInterceptor,
        authInterceptor,
        mobileNativeHttpInterceptor,
        mobileSessionInterceptor,
        hisHopeErrorInterceptor,
      ]),
    ),
    { provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler },
    { provide: AbstractSecurityStorage, useClass: MobileSecureOidcStorage },
    // Hydrates persisted tokens from Keychain/Keystore before the router's
    // initial navigation runs checkAuth() / the auth guard.
    provideAppInitializer(() => inject(MobileSecureOidcStorage).hydrate()),
    provideAppInitializer(() =>
      inject(MobilePlatformService).configureCertificatePins(),
    ),
    provideAuth({
      config: {
        authority: environment.oidc.authority,
        // The local gateway advertises localhost in discovery. That host is
        // the emulator itself, so native clients must use the host alias for
        // authorization, token, logout, and JWKS requests.
        authWellknownEndpoints: {
          authorizationEndpoint: `${environment.oidc.authority}/connect/authorize`,
          tokenEndpoint: `${environment.oidc.authority}/connect/token`,
          endSessionEndpoint: `${environment.oidc.authority}/connect/logout`,
          revocationEndpoint: `${environment.oidc.authority}/connect/revoke`,
          jwksUri: `${environment.oidc.authority}/.well-known/jwks`,
        },
        redirectUrl: environment.oidc.redirectUrl,
        postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
        clientId: environment.oidc.clientId,
        scope: environment.oidc.scope,
        responseType: "code",
        silentRenew: false,
        useRefreshToken: true,
        secureRoutes: environment.oidc.secureRoutes,
        autoUserInfo: false,
        logLevel: environment.production ? 0 : 1,
      },
    }),
  ],
};
