import {
  ApplicationConfig,
  ErrorHandler,
  PLATFORM_ID,
  provideZoneChangeDetection,
} from "@angular/core";
import { DOCUMENT } from "@angular/common";
import { provideRouter } from "@angular/router";
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
  withInterceptorsFromDi,
  HTTP_INTERCEPTORS,
} from "@angular/common/http";
import { provideAnimations } from "@angular/platform-browser/animations";
import { provideStore } from "@ngrx/store";
import { provideEffects } from "@ngrx/effects";
import { provideStoreDevtools } from "@ngrx/store-devtools";
import { provideAuth, LogLevel } from "angular-auth-oidc-client";

import { routes } from "./app.routes";
import { authReducer } from "@store/auth/auth.reducer";
import { patientsReducer } from "@store/patients/patients.reducer";
import { errorReducer } from "@store/error/error.reducer";
import { AuthEffects } from "@store/auth/auth.effects";
import { PatientsEffects } from "@store/patients/patients.effects";
import { csrfInterceptor } from "@core/interceptors/csrf.interceptor";
import { authInterceptor } from "@core/interceptors/auth.interceptor";
import { ErrorInterceptor } from "@core/interceptors/error.interceptor";
import { GlobalErrorHandler } from "@core/errors/global-error-handler";

import { environment } from "@env/environment";
import { mockServiceProviders } from "@core/services/mock/mock-providers";
import {
  HisHopeI18nService,
  HisHopeLocalizationApiService,
  HIS_HOPE_LOCALIZATION_API_URL,
  hisHopeInternationalizationInterceptor,
} from "@his-hope/frontend-foundation/i18n";
import { hisHopeCookieSessionInterceptor } from "@his-hope/frontend-foundation";
import { RuntimeConfigService } from "@his-hope/frontend-foundation";

function defaultClinicalRuntimeSource() {
  return (
    window.__HISHOPE_CONFIG__ ?? {
      apiOrigin: environment.oidc.authority,
      oidcAuthority: environment.oidc.authority,
      // The .local profile is an explicit lab-only HTTP compatibility mode.
      // Real production origins remain HTTPS and keep the production guard.
      production:
        environment.production &&
        !window.location.hostname.endsWith(".his-hope.local"),
    }
  );
}

export function createClinicalRuntimeConfig(
  source = defaultClinicalRuntimeSource(),
) {
  return new RuntimeConfigService(source).requireWeb({
    clientId: environment.oidc.clientId,
    scope: environment.oidc.scope,
    secureRoutes: [environment.apiUrl + "/"],
    redirectPath: "/auth/callback",
    postLogoutRedirectPath: "/auth/login",
    silentRenewPath: "/auth/silent-refresh",
    responseType: environment.oidc.responseType,
    maxIdTokenIatOffsetInSeconds: environment.oidc.maxIdTokenIatOffsetInSeconds,
    usePkce: environment.oidc.usePkce,
  });
}

const runtime = createClinicalRuntimeConfig();

export const appConfig: ApplicationConfig = {
  providers: [
    {
      provide: HIS_HOPE_LOCALIZATION_API_URL,
      useValue: runtime.localizationApiUrl,
    },
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
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptorsFromDi(),
      withInterceptors([
        hisHopeInternationalizationInterceptor,
        hisHopeCookieSessionInterceptor,
        authInterceptor,
        csrfInterceptor,
      ]),
    ),
    provideAuth({
      config: {
        authority: runtime.oidcAuthority,
        redirectUrl: runtime.redirectUrl,
        postLogoutRedirectUri: runtime.postLogoutRedirectUri,
        clientId: runtime.clientId,
        scope: runtime.scope,
        responseType: runtime.responseType,
        silentRenew: true,
        useRefreshToken: false,
        silentRenewUrl: runtime.silentRenewUrl,
        renewTimeBeforeTokenExpiresInSeconds: 120,
        secureRoutes: runtime.secureRoutes,
        triggerAuthorizationResultEvent: true,
        logLevel: runtime.production ? LogLevel.None : LogLevel.Debug,
        ignoreNonceAfterRefresh: true,
        historyCleanupOff: true,
        maxIdTokenIatOffsetAllowedInSeconds:
          runtime.maxIdTokenIatOffsetInSeconds,
        autoUserInfo: false,
      },
    }),
    provideAnimations(),
    provideStore({
      auth: authReducer,
      patients: patientsReducer,
      error: errorReducer,
    }),
    provideEffects([AuthEffects, PatientsEffects]),
    provideStoreDevtools({ maxAge: 25 }),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
    ...(environment.useMockServices ? mockServiceProviders : []),
  ],
};
