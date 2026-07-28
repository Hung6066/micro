import { inject } from "@angular/core";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { createHisHopeBearerTokenInterceptor } from "@his-hope/frontend-foundation";

export const authInterceptor = createHisHopeBearerTokenInterceptor(
  () => inject(OidcSecurityService).getAccessToken(),
  { withCredentials: false },
);
