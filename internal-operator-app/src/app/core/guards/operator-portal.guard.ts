import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { map, take } from "rxjs";

/** Internal operator shells accept only portal_class=operator (ADR 017). */
export const operatorPortalGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  return oidc.getPayloadFromAccessToken().pipe(
    take(1),
    map((payload) => {
      const portalClass =
        (payload as { portal_class?: string } | null)?.portal_class ?? "operator";
      if (
        portalClass === "customer_operator" ||
        portalClass === "end_user"
      ) {
        return router.parseUrl("/auth/login");
      }
      return true;
    }),
  );
};
