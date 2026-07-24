import { inject, Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivate, RouterStateSnapshot } from '@angular/router';
import { Observable, filter, switchMap, tap } from 'rxjs';
import { AuthService } from '@core/services/auth.service';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  private authService = inject(AuthService);
  private oidcSecurityService = inject(OidcSecurityService);

  canActivate(_route: ActivatedRouteSnapshot, _state: RouterStateSnapshot): Observable<boolean> {
    return this.authService.checkAuth().pipe(
      switchMap(() => this.authService.isLoggedIn()),
      tap(isAuth => { if (!isAuth) this.oidcSecurityService.authorize(); }),
      filter(isAuth => isAuth),
    );
  }
}
