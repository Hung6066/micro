import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { map, take } from "rxjs";

/** ADR 017: buyer app accepts only end_user portal tokens. */
export const endUserPortalGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  return oidc.getPayloadFromAccessToken().pipe(
    take(1),
    map((payload) => {
      const portalClass =
        (payload as { portal_class?: string } | null)?.portal_class ?? "";
      if (portalClass === "end_user" || portalClass === "") {
        return true;
      }
      return router.parseUrl("/auth/login");
    }),
  );
};
