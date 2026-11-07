import { inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { CanActivateFn, Router } from "@angular/router";
import { catchError, map, of, take } from "rxjs";

interface BffSessionStatus {
  authenticated: boolean;
  portalClass?: string;
}

/** Internal operator shells accept only portal_class=operator (ADR 017). */
export const operatorPortalGuard: CanActivateFn = () => {
  const http = inject(HttpClient);
  const router = inject(Router);
  return http.get<BffSessionStatus>("/api/v1/auth/session-status", { withCredentials: true }).pipe(
    take(1),
    map((session) => {
      const portalClass = session.portalClass ?? "operator";
      if (
        portalClass === "customer_operator" ||
        portalClass === "end_user"
      ) {
        return router.parseUrl("/auth/login");
      }
      return true;
    }),
    catchError(() => of(router.parseUrl("/auth/login"))),
  );
};
