import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { HisHopeAuthCoordinator } from '@his-hope/frontend-foundation/auth';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly coordinator = new HisHopeAuthCoordinator(
    inject(OidcSecurityService),
    inject(HttpClient),
    inject(Router),
    {
      defaultReturnUrl: '/resources',
      sessionStatusUrl: `${environment.authApiUrl}/session-status`,
      sessionExchangeUrl: `${environment.authApiUrl}/session/exchange`,
      logoutUrl: `${environment.authApiUrl}/logout`,
      bffOnly: true,
    },
  );

  readonly isAuthenticated$: Observable<boolean> =
    this.coordinator.isAuthenticated$;

  /** Wait for initial OIDC checkAuth to complete (used by guards) */
  checkAuth(): Observable<void> {
    return this.coordinator.checkAuth();
  }

  login(returnUrl?: string): void {
    this.coordinator.login(returnUrl);
  }

  oidcLogin(): void {
    this.coordinator.oidcLogin();
  }

  oidcLogout(): void {
    this.coordinator.logout();
  }

  logout(): void {
    this.coordinator.logout();
  }

  trySsoLogin(returnUrl?: string): Observable<boolean> {
    return this.coordinator.trySsoLogin(returnUrl);
  }

  getAccessToken(): Observable<string> {
    return this.coordinator.getAccessToken();
  }

  refreshAccessToken(): Observable<boolean> {
    return this.coordinator.refreshAccessToken();
  }

  handleSessionExpired(): void {
    this.coordinator.handleSessionExpired();
  }

  handleCallback(): Observable<boolean> {
    return this.coordinator.handleCallback();
  }
}
