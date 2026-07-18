import { ApplicationConfig, ErrorHandler } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { provideStoreDevtools } from '@ngrx/store-devtools';

import { routes } from './app.routes';
import { authReducer } from '@store/auth/auth.reducer';
import { patientsReducer } from '@store/patients/patients.reducer';
import { errorReducer } from '@store/error/error.reducer';
import { AuthEffects } from '@store/auth/auth.effects';
import { PatientsEffects } from '@store/patients/patients.effects';
import { AuthInterceptor } from '@core/interceptors/auth.interceptor';
import { ErrorInterceptor } from '@core/interceptors/error.interceptor';
import { GlobalErrorHandler } from '@core/errors/global-error-handler';

import { environment } from '@env/environment';
import { mockServiceProviders } from '@core/services/mock/mock-providers';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptorsFromDi()),
    provideAnimations(),
    provideStore({
      auth: authReducer,
      patients: patientsReducer,
      error: errorReducer,
    }),
    provideEffects([AuthEffects, PatientsEffects]),
    provideStoreDevtools({ maxAge: 25 }),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
    ...(environment.useMockServices ? mockServiceProviders : []),
  ],
};
