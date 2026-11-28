import { ApplicationConfig, ErrorHandler, PLATFORM_ID } from "@angular/core";
import { DOCUMENT } from "@angular/common";
import { provideRouter } from "@angular/router";
import { provideAnimations } from "@angular/platform-browser/animations";
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from "@angular/common/http";
import { provideAuth } from "angular-auth-oidc-client";
import {
  hisHopeCorrelationIdInterceptor,
  hisHopeErrorInterceptor,
  HisHopeGlobalErrorHandler,
  hisHopeCookieSessionInterceptor,
  RuntimeConfigService,
} from "@his-hope/frontend-foundation";
import {
  hisHopeInternationalizationInterceptor,
  HisHopeI18nService,
  HisHopeLocalizationApiService,
  HIS_HOPE_LOCALIZATION_API_URL,
} from "@his-hope/frontend-foundation/i18n";
import { routes } from "./app.routes";
import { authInterceptor } from "./core/services/auth-interceptor.service";
import { tenantScopeInterceptor } from "./core/interceptors/tenant-scope.interceptor";
import { commerceTenantInterceptor } from "./core/interceptors/commerce-tenant.interceptor";
import { contentTenantInterceptor } from "./core/interceptors/content-tenant.interceptor";
import { manufacturingTenantInterceptor } from "./core/interceptors/manufacturing-tenant.interceptor";
import { environment } from "../environments/environment";
import { operatorContentTranslations } from "./core/i18n/operator-content-translations";
import { operatorCommerceTranslations } from "./core/i18n/operator-commerce-translations";

function defaultRuntimeSource() {
  const browserDefault =
    typeof window !== "undefined"
      ? {
          apiOrigin: window.location.origin,
          oidcAuthority: window.location.origin,
          production: false,
        }
      : {
          apiOrigin: "http://localhost:5000",
          oidcAuthority: "http://localhost:5000",
          production: false,
        };

  return window.__HISHOPE_CONFIG__ ?? browserDefault;
}

export function createInternalOperatorRuntimeConfig(
  source = defaultRuntimeSource(),
) {
  return new RuntimeConfigService(source).requireWeb({
    clientId: environment.oidc.clientId,
    scope: environment.oidc.scope,
    secureRoutes: environment.oidc.secureRoutes,
    redirectPath: "/auth/callback",
    postLogoutRedirectPath: "/auth/login",
    silentRenewPath: "/auth/silent-refresh",
    responseType: environment.oidc.responseType,
    maxIdTokenIatOffsetInSeconds: environment.oidc.maxIdTokenIatOffsetInSeconds,
  });
}

const runtime = createInternalOperatorRuntimeConfig();

export const appConfig: ApplicationConfig = {
  providers: [
    {
      provide: HisHopeI18nService,
      useFactory: (document: Document, platformId: object) => {
        const i18n = new HisHopeI18nService(document, platformId);
        for (const [locale, translations] of Object.entries(operatorContentTranslations)) {
          i18n.registerTranslations(locale, translations);
        }
        for (const [locale, translations] of Object.entries(operatorCommerceTranslations)) {
          i18n.registerTranslations(locale, translations);
        }
        return i18n;
      },
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
      useValue: runtime.localizationApiUrl,
    },
    provideRouter(routes),
    provideAnimations(),
    provideHttpClient(
      withInterceptors([
        hisHopeCorrelationIdInterceptor,
        hisHopeInternationalizationInterceptor,
        hisHopeCookieSessionInterceptor,
        tenantScopeInterceptor,
        commerceTenantInterceptor,
        contentTenantInterceptor,
        manufacturingTenantInterceptor,
        authInterceptor,
        hisHopeErrorInterceptor,
      ]),
    ),
    { provide: ErrorHandler, useClass: HisHopeGlobalErrorHandler },
    provideAuth({
      config: {
        authority: runtime.oidcAuthority,
        redirectUrl: runtime.redirectUrl,
        postLogoutRedirectUri: runtime.postLogoutRedirectUri,
        clientId: runtime.clientId,
        scope: runtime.scope,
        responseType: runtime.responseType,
        // Manufacturing operator uses the BFF HttpOnly session contract. Do not
        // start the OIDC silent-renew iframe, which has no browser access token
        // in this mode and otherwise validates an empty token on every load.
        silentRenew: false,
        startCheckSession: false,
        useRefreshToken: false,
        silentRenewUrl: runtime.silentRenewUrl,
        renewTimeBeforeTokenExpiresInSeconds: 120,
        secureRoutes: runtime.secureRoutes,
        maxIdTokenIatOffsetAllowedInSeconds:
          runtime.maxIdTokenIatOffsetInSeconds,
        logLevel: runtime.production ? 0 : 3,
        autoUserInfo: false,
      },
    }),
  ],
};
