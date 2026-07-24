import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { provideAuth } from 'angular-auth-oidc-client';
import { routes } from './app.routes';
import { authInterceptor } from './core/services/auth-interceptor.service';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(withInterceptors([authInterceptor])),
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
        maxIdTokenIatOffsetAllowedInSeconds: environment.oidc.maxIdTokenIatOffsetInSeconds,
        logLevel: environment.production ? 0 : 1,
        autoUserInfo: false,
      },
    }),
  ],
};
