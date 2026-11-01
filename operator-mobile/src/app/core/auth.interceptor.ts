import { inject } from "@angular/core";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { createHisHopeBearerTokenInterceptor } from "@his-hope/frontend-foundation";

export const authInterceptor = createHisHopeBearerTokenInterceptor(
  () => inject(OidcSecurityService).getAccessToken(),
  {
    withCredentials: false,
    // The mobile client is DPoP-bound in every environment. Browser preview
    // uses the same Web Crypto proof service; native builds additionally use
    // the pinned platform transport.
    tokenType: "DPoP",
  },
);
