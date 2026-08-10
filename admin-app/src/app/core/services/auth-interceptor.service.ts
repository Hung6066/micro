import { inject } from "@angular/core";
import { createHisHopeBearerTokenInterceptor } from "@his-hope/frontend-foundation";
import { AuthService } from "./auth.service";

export const authInterceptor = createHisHopeBearerTokenInterceptor(
  () => inject(AuthService).getAccessToken(),
  {
    // BFF-only: the coordinator returns no browser token. Requests use the
    // HttpOnly hishop_sid cookie and the gateway attaches the token server-side.
    matches: (url) => url.includes("/api/"),
    refreshAccessToken: () => inject(AuthService).refreshAccessToken(),
    onSessionExpired: () => inject(AuthService).handleSessionExpired(),
  },
);
