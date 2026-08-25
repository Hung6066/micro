import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { createHisHopeBearerTokenInterceptor } from "@his-hope/frontend-foundation";
import { AuthService } from "./auth.service";

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const authService = inject(AuthService);
  return createHisHopeBearerTokenInterceptor(
    () => authService.getAccessToken(),
    {
      matches: (url) => url.includes("/api/"),
      refreshAccessToken: () => authService.refreshAccessToken(),
      onSessionExpired: () => authService.handleSessionExpired(),
    },
  )(request, next);
};
