import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { map, take } from "rxjs";
import { AuthService } from "../services/auth.service";

/** ADR 017: buyer shell uses BFF cookies; CommerceService enforces portal_class=end_user. */
export const endUserPortalGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.isAuthenticated$.pipe(
    take(1),
    map((isAuthenticated) => (isAuthenticated ? true : router.parseUrl("/auth/login"))),
  );
};
