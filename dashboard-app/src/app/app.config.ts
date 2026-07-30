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
} from '@his-hope/frontend-foundation';
import { routes } from './app.routes';
import { authInterceptor } from './core/services/auth-interceptor.service';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: HIS_HOPE_LOCALIZATION_API_URL, useValue: `${environment.oidc.authority}/api/v1` },
    { provide: HisHopeI18nService, useFactory: (document: Document, platformId: object) => new HisHopeI18nService(document, platformId), deps: [DOCUMENT, PLATFORM_ID] },
    { provide: HisHopeLocalizationApiService, useFactory: (http: HttpClient, i18n: HisHopeI18nService, apiUrl: string) => new HisHopeLocalizationApiService(http, i18n, apiUrl), deps: [HttpClient, HisHopeI18nService, HIS_HOPE_LOCALIZATION_API_URL] },
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(
      withInterceptors([
        hisHopeCorrelationIdInterceptor,
        hisHopeInternationalizationInterceptor,
        authInterceptor,
        hisHopeErrorInterceptor,
      ]),
    ),
    { provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler },
    importProvidersFrom(MatSnackBarModule),
    provideAuth({
      config: {
        authority: environment.oidc.authority,
        redirectUrl: environment.oidc.redirectUrl,
        postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
        clientId: environment.oidc.clientId,
        scope: environment.oidc.scope,
        responseType: environment.oidc.responseType,
        silentRenew: true,
        useRefreshToken: true,
        silentRenewUrl: environment.oidc.silentRenewUrl,
        renewTimeBeforeTokenExpiresInSeconds: 120,
        secureRoutes: environment.oidc.secureRoutes,
        maxIdTokenIatOffsetAllowedInSeconds:
          environment.oidc.maxIdTokenIatOffsetInSeconds,
        logLevel: environment.production ? 0 : 1,
        autoUserInfo: false,
      },
    }),
  ],
};
