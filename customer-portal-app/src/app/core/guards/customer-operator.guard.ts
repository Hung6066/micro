import { inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { CanActivateFn, Router } from "@angular/router";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { catchError, map, of, switchMap, take } from "rxjs";

interface AdminPermissionsProfile {
  tenantMemberships?: string[];
}

/** ADR 017: customer portal accepts only customer_operator tokens. */
export const customerOperatorGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  const http = inject(HttpClient);

  return oidc.getPayloadFromAccessToken().pipe(
    take(1),
    switchMap((payload) => {
      const portalClass =
        (payload as { portal_class?: string } | null)?.portal_class ?? "";

      if (portalClass === "customer_operator") {
        return of(true);
      }

      if (portalClass === "operator" || portalClass === "end_user") {
        return of(router.parseUrl("/auth/login"));
      }

      // BFF cookie sessions do not expose access tokens in the browser.
      // Ask Identity whether this session can use the customer admin shell.
      return http.get<AdminPermissionsProfile>("/api/v1/admin/me/permissions").pipe(
        map((profile) =>
          (profile.tenantMemberships?.length ?? 0) > 0
            ? true
            : router.parseUrl("/auth/login"),
        ),
        catchError(() => of(router.parseUrl("/auth/login"))),
      );
    }),
  );
};
