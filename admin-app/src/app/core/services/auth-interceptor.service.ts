import { inject } from "@angular/core";
import { createHisHopeBearerTokenInterceptor } from "@his-hope/frontend-foundation";
import { AuthService } from "./auth.service";

export const authInterceptor = createHisHopeBearerTokenInterceptor(
  () => inject(AuthService).getAccessToken(),
  {
    // All API endpoints, including admin routes, use the OIDC access token.
    // The gateway must not replace this token with the legacy hishop_sid JWE.
    matches: (url) => url.includes("/api/"),
  },
);
