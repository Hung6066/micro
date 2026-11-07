import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { map, switchMap } from "rxjs";
import { AuthService } from "../services/auth.service";

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.checkAuth().pipe(
    switchMap(() => auth.isAuthenticated$),
    map((isAuth) => (isAuth ? true : router.parseUrl("/auth/login"))),
  );
};
