import { NgModule, ErrorHandler } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { StoreModule } from '@ngrx/store';
import { EffectsModule } from '@ngrx/effects';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { environment } from '@env/environment';
import { AuthInterceptor } from '@core/interceptors/auth.interceptor';
import { ErrorInterceptor } from '@core/interceptors/error.interceptor';
import { SharedModule } from '@shared/shared.module';
import { authReducer } from '@store/auth/auth.reducer';
import { patientsReducer } from '@store/patients/patients.reducer';
import { errorReducer } from '@store/error/error.reducer';
import { AuthEffects } from '@store/auth/auth.effects';
import { PatientsEffects } from '@store/patients/patients.effects';
import { GlobalErrorHandler } from '@core/errors/global-error-handler';

/** Conditionally load mock providers when no backend is available */
import { mockServiceProviders } from '@core/services/mock/mock-providers';

@NgModule({ declarations: [AppComponent],
    bootstrap: [AppComponent], imports: [BrowserModule,
        BrowserAnimationsModule,
        AppRoutingModule,
        SharedModule,
        StoreModule.forRoot({
            auth: authReducer,
            patients: patientsReducer,
            error: errorReducer,
        }),
        EffectsModule.forRoot([AuthEffects, PatientsEffects]),
        !environment.production ? StoreDevtoolsModule.instrument() : []], providers: [
        { provide: ErrorHandler, useClass: GlobalErrorHandler },
        { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
        ...(environment.useMockServices ? mockServiceProviders : []),
        provideHttpClient(withInterceptorsFromDi()),
    ] })
export class AppModule {}
