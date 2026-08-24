import { Injectable, inject } from "@angular/core";
import { HttpBackend, HttpClient } from "@angular/common/http";
import { Router } from "@angular/router";
import { Observable } from "rxjs";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { HisHopeAuthCoordinator } from "@his-hope/frontend-foundation/auth";

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly bffHttpClient = new HttpClient(inject(HttpBackend));
  private readonly coordinator = new HisHopeAuthCoordinator(
    inject(OidcSecurityService),
    this.bffHttpClient,
    inject(Router),
    {
      defaultReturnUrl: "/dashboard",
      sessionStatusUrl: "/api/v1/auth/session-status",
      bffOnly: true,
    },
  );

  readonly isAuthenticated$ = this.coordinator.isAuthenticated$;

  checkAuth(): Observable<void> {
    return this.coordinator.checkAuth();
  }

  login(returnUrl?: string): void {
    this.coordinator.login(returnUrl);
  }

  trySsoLogin(returnUrl?: string): Observable<boolean> {
    return this.coordinator.trySsoLogin(returnUrl);
  }

  logout(): void {
    this.coordinator.logout();
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
