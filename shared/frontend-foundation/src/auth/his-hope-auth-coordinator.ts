import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { EMPTY, Observable, ReplaySubject, of, timer } from 'rxjs';
import { catchError, exhaustMap, finalize, map, switchMap, take, tap } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';

export interface HisHopeAuthOptions {
  defaultReturnUrl: string;
  loginRoute?: string;
  sessionStatusUrl?: string;
  sessionExchangeUrl?: string;
  logoutUrl?: string;
}

export interface IdentitySessionStatus {
  authenticated: boolean;
  userName?: string;
}

const AUTH_CHANNEL = 'hishop_auth';
const RETURN_URL_KEY = 'auth_return_url';
const SSO_LOGIN_IN_PROGRESS_KEY = 'hishop_sso_login_in_progress_until';
const SSO_SUPPRESSED_UNTIL_KEY = 'hishop_sso_suppressed_until';

export class HisHopeAuthCoordinator {
  private readonly authenticatedSubject = new ReplaySubject<boolean>(1);
  private readonly checkAuthInit$ = new ReplaySubject<void>(1);
  private readonly sessionStatusUrl: string;
  private readonly sessionExchangeUrl: string;
  private readonly logoutUrl: string;
  private readonly defaultReturnUrl: string;
  private readonly loginRoute: string;
  private lastAuthenticated = false;
  private ssoLoginInProgress = false;

  readonly isAuthenticated$: Observable<boolean> = this.authenticatedSubject.asObservable();

  constructor(
    private readonly oidcSecurityService: OidcSecurityService,
    private readonly http: HttpClient,
    private readonly router: Router,
    options: HisHopeAuthOptions,
  ) {
    this.defaultReturnUrl = options.defaultReturnUrl;
    this.loginRoute = options.loginRoute ?? '/auth/login';
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    this.sessionStatusUrl =
      options.sessionStatusUrl ??
      (origin ? `${window.location.protocol}//${window.location.hostname}:5000/api/v1/auth/session-status` : '/api/v1/auth/session-status');
    this.sessionExchangeUrl = options.sessionExchangeUrl ?? this.sessionStatusUrl.replace(/\/session-status$/, '/session/exchange');
    this.logoutUrl = options.logoutUrl ?? this.sessionStatusUrl.replace(/\/session-status$/, '/logout');

    this.oidcSecurityService.isAuthenticated$.subscribe((result) => {
      this.lastAuthenticated = result.isAuthenticated;
      this.authenticatedSubject.next(result.isAuthenticated);
    });

    if (!this.isAuthCallbackRoute()) {
      this.oidcSecurityService
        .checkAuth()
        .pipe(
          take(1),
          switchMap((result) => result.isAuthenticated ? this.exchangeBffSession() : of(void 0)),
          finalize(() => this.completeCheckAuthInit()),
        )
        .subscribe();
    }

    this.initBroadcastChannel();
    this.startSsoSessionMonitor();
  }

  checkAuth(): Observable<void> {
    return this.checkAuthInit$.asObservable();
  }

  login(returnUrl?: string): void {
    const safeReturnUrl = this.safeReturnUrl(returnUrl);
    if (safeReturnUrl) localStorage.setItem(RETURN_URL_KEY, safeReturnUrl);
    else localStorage.removeItem(RETURN_URL_KEY);
    this.oidcLogin();
  }

  oidcLogin(): void {
    this.markSsoLoginInProgress();
    this.oidcSecurityService.authorize();
  }

  logout(): void {
    this.markSsoLogout();
    this.broadcastLogout();
    localStorage.removeItem(RETURN_URL_KEY);
    this.http.post(this.logoutUrl, {}, { withCredentials: true }).pipe(
      catchError(() => of(void 0)),
      finalize(() => this.oidcSecurityService.logoff().subscribe()),
    ).subscribe();
  }

  trySsoLogin(returnUrl?: string): Observable<boolean> {
    if (this.isSsoLoginInProgress() || this.isSsoSuppressed()) {
      return of(false);
    }

    return this.getIdentitySessionStatus().pipe(
      take(1),
      tap((status) => {
        if (status.authenticated) {
          this.login(returnUrl);
        }
      }),
      map((status) => status.authenticated),
      catchError(() => of(false)),
    );
  }

  getAccessToken(): Observable<string> {
    return this.oidcSecurityService.getAccessToken();
  }

  handleCallback(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      switchMap(({ isAuthenticated }) => isAuthenticated
        ? this.exchangeBffSession().pipe(map(() => true))
        : of(false)),
      tap((isAuthenticated) => {
        this.clearSsoLoginInProgress();
        if (isAuthenticated) {
          const returnUrl = this.safeReturnUrl(localStorage.getItem(RETURN_URL_KEY)) ?? this.safeReturnUrl(this.defaultReturnUrl) ?? '/';
          localStorage.removeItem(RETURN_URL_KEY);
          this.router.navigateByUrl(returnUrl);
        } else {
          this.router.navigate([this.loginRoute]);
        }
      }),
      finalize(() => this.completeCheckAuthInit()),
    );
  }

  private initBroadcastChannel(): void {
    try {
      const channel = new BroadcastChannel(AUTH_CHANNEL);
      channel.onmessage = (event: MessageEvent) => {
        if (event.data?.type === 'LOGOUT') {
          this.authenticatedSubject.next(false);
          this.oidcSecurityService.logoffLocal();
          if (!this.router.url.includes(this.loginRoute)) {
            this.router.navigate([this.loginRoute]);
          }
        }
      };
    } catch {
      /* BroadcastChannel not supported */
    }
  }

  private broadcastLogout(): void {
    try {
      const channel = new BroadcastChannel(AUTH_CHANNEL);
      channel.postMessage({ type: 'LOGOUT' });
      channel.close();
    } catch {
      /* BroadcastChannel not supported */
    }
  }

  private startSsoSessionMonitor(): void {
    timer(5000, 10000)
      .pipe(
        exhaustMap(() => {
          if (!this.lastAuthenticated || (typeof document !== 'undefined' && document.visibilityState === 'hidden')) {
            return EMPTY;
          }
          return this.getIdentitySessionStatus().pipe(catchError(() => EMPTY));
        }),
      )
      .subscribe((status) => {
        if (!status.authenticated) {
          this.forceLocalLogout();
        }
      });
  }

  private getIdentitySessionStatus(): Observable<IdentitySessionStatus> {
    return this.http.get<IdentitySessionStatus>(this.sessionStatusUrl, { withCredentials: true });
  }

  private exchangeBffSession(): Observable<void> {
    return this.http.post<void>(this.sessionExchangeUrl, {}, { withCredentials: true }).pipe(
      map(() => void 0),
      catchError(() => of(void 0)),
    );
  }

  private forceLocalLogout(): void {
    this.markSsoLogout();
    this.broadcastLogout();
    localStorage.removeItem(RETURN_URL_KEY);
    this.oidcSecurityService.logoffLocal();
    this.lastAuthenticated = false;
    this.authenticatedSubject.next(false);
    if (!this.router.url.includes(this.loginRoute)) {
      this.router.navigate([this.loginRoute]);
    }
  }

  private markSsoLogout(): void {
    sessionStorage.setItem(SSO_SUPPRESSED_UNTIL_KEY, String(Date.now() + 10000));
  }

  private isSsoSuppressed(): boolean {
    const until = Number(sessionStorage.getItem(SSO_SUPPRESSED_UNTIL_KEY) ?? 0);
    return Date.now() < until;
  }

  private markSsoLoginInProgress(): void {
    this.ssoLoginInProgress = true;
    sessionStorage.setItem(SSO_LOGIN_IN_PROGRESS_KEY, String(Date.now() + 15000));
  }

  private clearSsoLoginInProgress(): void {
    sessionStorage.removeItem(SSO_LOGIN_IN_PROGRESS_KEY);
    this.ssoLoginInProgress = false;
  }

  private isSsoLoginInProgress(): boolean {
    const until = Number(sessionStorage.getItem(SSO_LOGIN_IN_PROGRESS_KEY) ?? 0);
    return this.ssoLoginInProgress || Date.now() < until;
  }

  private isAuthCallbackRoute(): boolean {
    return window.location.pathname.includes('/auth/callback');
  }

  private safeReturnUrl(value: string | null | undefined): string | null {
    if (!value || typeof window === 'undefined') return null;
    try {
      const decoded = decodeURIComponent(value).trim();
      if (!decoded.startsWith('/') || decoded.startsWith('//') || decoded.includes('\\') || /[\u0000-\u001f]/.test(decoded)) return null;
      const url = new URL(decoded, window.location.origin);
      if (url.origin !== window.location.origin) return null;
      return `${url.pathname}${url.search}${url.hash}`;
    } catch {
      return null;
    }
  }

  private completeCheckAuthInit(): void {
    this.checkAuthInit$.next();
    this.checkAuthInit$.complete();
  }
}
