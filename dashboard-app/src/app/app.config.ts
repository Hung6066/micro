import {
  ApplicationConfig,
  ErrorHandler,
  importProvidersFrom,
  PLATFORM_ID,
} from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { provideAuth } from 'angular-auth-oidc-client';
import {
  hisHopeCorrelationIdInterceptor,
  hisHopeErrorInterceptor,
  HisHopeGlobalErrorHandler,
  hisHopeInternationalizationInterceptor,
  HisHopeI18nService,
  HisHopeLocalizationApiService,
  HIS_HOPE_LOCALIZATION_API_URL,
  hisHopeCookieSessionInterceptor,
} from '@his-hope/frontend-foundation';
import { RuntimeConfigService } from '@his-hope/frontend-foundation';
import { routes } from './app.routes';
import { authInterceptor } from './core/services/auth-interceptor.service';
import { environment } from '../environments/environment';

function defaultDashboardRuntimeSource() {
  return (
    window.__HISHOPE_CONFIG__ ?? {
      apiOrigin: environment.oidc.authority,
      oidcAuthority: environment.oidc.authority,
      production: environment.production,
    }
  );
}

export function createDashboardRuntimeConfig(source = defaultDashboardRuntimeSource()) {
  return new RuntimeConfigService(source).requireWeb({
    clientId: environment.oidc.clientId,
    scope: environment.oidc.scope,
    secureRoutes: environment.oidc.secureRoutes,
    redirectPath: "/auth/callback",
    postLogoutRedirectPath: "/auth/login",
    silentRenewPath: "/auth/silent-refresh",
    responseType: environment.oidc.responseType,
    maxIdTokenIatOffsetInSeconds:
      environment.oidc.maxIdTokenIatOffsetInSeconds,
  });
}

const runtime = createDashboardRuntimeConfig();

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: HIS_HOPE_LOCALIZATION_API_URL, useValue: runtime.localizationApiUrl },
    { provide: HisHopeI18nService, useFactory: (document: Document, platformId: object) => new HisHopeI18nService(document, platformId), deps: [DOCUMENT, PLATFORM_ID] },
    { provide: HisHopeLocalizationApiService, useFactory: (http: HttpClient, i18n: HisHopeI18nService, apiUrl: string) => new HisHopeLocalizationApiService(http, i18n, apiUrl), deps: [HttpClient, HisHopeI18nService, HIS_HOPE_LOCALIZATION_API_URL] },
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(
      withInterceptors([
        hisHopeCorrelationIdInterceptor,
        hisHopeInternationalizationInterceptor,
        hisHopeCookieSessionInterceptor,
        authInterceptor,
        hisHopeErrorInterceptor,
      ]),
    ),
    { provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler },
    importProvidersFrom(MatSnackBarModule),
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
        maxIdTokenIatOffsetAllowedInSeconds:
          runtime.maxIdTokenIatOffsetInSeconds,
        logLevel: runtime.production ? 0 : 1,
        autoUserInfo: false,
      },
    }),
  ],
};
