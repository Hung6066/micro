import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, map, shareReplay } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class MobileAuthService {
  private readonly oidc = inject(OidcSecurityService);
  readonly isAuthenticated$ = this.oidc.isAuthenticated$.pipe(map(state => state.isAuthenticated));
  readonly userData$ = this.oidc.userData$;
  private readonly authCheck$ = this.oidc.checkAuth().pipe(
    map(result => result.isAuthenticated),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  checkAuth(): Observable<boolean> { return this.authCheck$; }
  ensureAuthenticated(): Observable<boolean> { return this.authCheck$; }
  login(): void { this.oidc.authorize(); }
  logout(): void { this.oidc.logoff().subscribe(); }
}
