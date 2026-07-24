import { ApplicationConfig, ErrorHandler, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { provideStoreDevtools } from '@ngrx/store-devtools';
import { provideAuth, LogLevel } from 'angular-auth-oidc-client';

import { routes } from './app.routes';
import { authReducer } from '@store/auth/auth.reducer';
import { patientsReducer } from '@store/patients/patients.reducer';
import { errorReducer } from '@store/error/error.reducer';
import { AuthEffects } from '@store/auth/auth.effects';
import { PatientsEffects } from '@store/patients/patients.effects';
import { ErrorInterceptor } from '@core/interceptors/error.interceptor';
import { csrfInterceptor } from '@core/interceptors/csrf.interceptor';
import { authInterceptor } from '@core/interceptors/auth.interceptor';
import { GlobalErrorHandler } from '@core/errors/global-error-handler';

import { environment } from '@env/environment';
import { mockServiceProviders } from '@core/services/mock/mock-providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi(), withInterceptors([authInterceptor, csrfInterceptor])),
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
        secureRoutes: [environment.apiUrl + '/'],
        triggerAuthorizationResultEvent: true,
        logLevel: environment.production ? LogLevel.None : LogLevel.Debug,
        ignoreNonceAfterRefresh: true,
        historyCleanupOff: true,
        maxIdTokenIatOffsetAllowedInSeconds: environment.oidc.maxIdTokenIatOffsetInSeconds,
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
