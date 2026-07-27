import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAuth } from 'angular-auth-oidc-client';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAuth({ config: {
      authority: environment.oidc.authority,
      redirectUrl: environment.oidc.redirectUrl,
      postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
      clientId: environment.oidc.clientId,
      scope: environment.oidc.scope,
      responseType: 'code',
      silentRenew: false,
      useRefreshToken: true,
      secureRoutes: environment.oidc.secureRoutes,
      autoUserInfo: false,
      logLevel: environment.production ? 0 : 1,
    } }),
  ],
};
