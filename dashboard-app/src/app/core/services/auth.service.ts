import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, ReplaySubject } from 'rxjs';
import { map, take, tap } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly oidcSecurityService = inject(OidcSecurityService);
  private readonly router = inject(Router);

  private authenticatedSubject = new ReplaySubject<boolean>(1);
  private readonly checkAuthInit$ = new ReplaySubject<void>(1);

  readonly isAuthenticated$: Observable<boolean> = this.authenticatedSubject.asObservable();
  private static readonly AUTH_CHANNEL = 'hishop_auth';

  constructor() {
    this.oidcSecurityService.isAuthenticated$.subscribe(result => {
      this.authenticatedSubject.next(result.isAuthenticated);
    });

    this.oidcSecurityService.checkAuth().pipe(take(1)).subscribe({
      next: () => {
        this.checkAuthInit$.next();
        this.checkAuthInit$.complete();
      },
      error: () => {
        this.checkAuthInit$.next();
        this.checkAuthInit$.complete();
      },
    });

    // Listen for cross-tab logout events
    this.initBroadcastChannel();
  }

  /** Wait for initial OIDC checkAuth to complete (used by guards) */
  checkAuth(): Observable<void> {
    return this.checkAuthInit$.asObservable();
  }

  login(returnUrl?: string): void {
    if (returnUrl) {
      localStorage.setItem('auth_return_url', returnUrl);
    }
    this.oidcLogin();
  }

  oidcLogin(): void {
    this.oidcSecurityService.authorize();
  }

  oidcLogout(): void {
    this.broadcastLogout();
    this.oidcSecurityService.logoff().subscribe();
  }

  logout(): void {
    this.broadcastLogout();
    localStorage.removeItem('auth_return_url');
    this.oidcSecurityService.logoff().subscribe();
  }

  getAccessToken(): Observable<string> {
    return this.oidcSecurityService.getAccessToken();
  }

  handleCallback(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      map(({ isAuthenticated }) => isAuthenticated),
      tap(isAuthenticated => {
        if (isAuthenticated) {
          const returnUrl = localStorage.getItem('auth_return_url') ?? '/resources';
          localStorage.removeItem('auth_return_url');
          this.router.navigateByUrl(returnUrl);
        } else {
          this.router.navigate(['/auth/login']);
        }
      }),
    );
  }

  private initBroadcastChannel(): void {
    try {
      const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
      channel.onmessage = (event: MessageEvent) => {
        if (event.data?.type === 'LOGOUT') {
          this.authenticatedSubject.next(false);
          if (!this.router.url.includes('/auth/login')) {
            this.router.navigate(['/auth/login']);
          }
        }
      };
    } catch { /* BroadcastChannel not supported */ }
  }

  private broadcastLogout(): void {
    try {
      const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
      channel.postMessage({ type: 'LOGOUT' });
      channel.close();
    } catch { /* BroadcastChannel not supported */ }
  }
}
