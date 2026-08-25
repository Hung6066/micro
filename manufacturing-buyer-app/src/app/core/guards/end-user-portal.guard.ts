import { inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { CanActivateFn, Router } from "@angular/router";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { catchError, map, of, switchMap, take } from "rxjs";

interface CommerceSessionContext {
  portalClass?: string;
}

/** ADR 017: buyer app accepts only end_user portal tokens (fail closed). */
export const endUserPortalGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  const http = inject(HttpClient);

  return oidc.getPayloadFromAccessToken().pipe(
    take(1),
    switchMap((payload) => {
      const portalClass =
        (payload as { portal_class?: string } | null)?.portal_class ?? "";

      if (portalClass === "end_user") {
        return of(true as const);
      }

      if (portalClass === "operator" || portalClass === "customer_operator") {
        return of(router.parseUrl("/auth/login"));
      }

      // BFF cookie sessions do not expose access tokens in the browser.
      return http.get<CommerceSessionContext>("/api/v1/commerce/session").pipe(
        map((session) =>
          session.portalClass === "end_user"
            ? true
            : router.parseUrl("/auth/login"),
        ),
        catchError(() => of(router.parseUrl("/auth/login"))),
      );
    }),
  );
};
