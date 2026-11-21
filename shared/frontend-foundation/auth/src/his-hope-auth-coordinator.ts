import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { EMPTY, Observable, ReplaySubject, of, throwError, timer } from 'rxjs';
import { catchError, exhaustMap, finalize, map, shareReplay, switchMap, take, tap } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';

export interface HisHopeAuthOptions {
  defaultReturnUrl: string;
  loginRoute?: string;
  sessionStatusUrl?: string;
  mfaStatusUrl?: string;
  sessionExchangeUrl?: string;
  /** OIDC client identifier used to resolve the BFF portal/tenant claims. */
  bffClientId?: string;
  logoutUrl?: string;
  /** Use the server-side HttpOnly session contract instead of browser OIDC tokens. */
  bffOnly?: boolean;
}

export interface IdentitySessionStatus {
  authenticated: boolean;
  userName?: string;
}

export interface IdentityMfaStatus {
  enabled: boolean;
  requiresMfa: boolean;
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
  private readonly mfaStatusUrl: string;
  private readonly logoutUrl: string;
  private readonly defaultReturnUrl: string;
  private readonly loginRoute: string;
  private readonly bffOnly: boolean;
  private readonly bffClientId?: string;
  private lastAuthenticated = false;
  private ssoLoginInProgress = false;
  private refreshAccessTokenInFlight$?: Observable<boolean>;
  private exchangeBffSessionInFlight$?: Observable<void>;

  readonly isAuthenticated$: Observable<boolean> = this.authenticatedSubject.asObservable();

  constructor(
    private readonly oidcSecurityService: OidcSecurityService,
    private readonly http: HttpClient,
    private readonly router: Router,
    options: HisHopeAuthOptions,
  ) {
    this.defaultReturnUrl = options.defaultReturnUrl;
    this.loginRoute = options.loginRoute ?? '/auth/login';
    this.bffOnly = options.bffOnly ?? false;
    this.bffClientId = options.bffClientId;
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    this.sessionStatusUrl =
      options.sessionStatusUrl ??
      (origin ? `${origin}/api/v1/auth/session-status` : '/api/v1/auth/session-status');
    this.sessionExchangeUrl = options.sessionExchangeUrl ?? this.sessionStatusUrl.replace(/\/session-status$/, '/session/exchange');
    this.mfaStatusUrl = options.mfaStatusUrl ?? this.sessionStatusUrl.replace(/\/session-status$/, '/mfa/status');
    this.logoutUrl = options.logoutUrl ?? this.sessionStatusUrl.replace(/\/session-status$/, '/logout');

    if (!this.bffOnly) {
      this.oidcSecurityService.isAuthenticated$.subscribe((result) => {
        this.lastAuthenticated = result.isAuthenticated;
        this.authenticatedSubject.next(result.isAuthenticated);
      });
    }

    if (this.bffOnly) {
      this.getIdentitySessionStatus().pipe(
        take(1),
        switchMap((status) => status.authenticated
          ? this.exchangeBffSession().pipe(map(() => true))
          : of(false)),
        catchError(() => of(false)),
        tap((authenticated) => {
          this.lastAuthenticated = authenticated;
          this.authenticatedSubject.next(authenticated);
        }),
        finalize(() => this.completeCheckAuthInit()),
      ).subscribe();
    } else if (!this.isAuthCallbackRoute()) {
      this.oidcSecurityService
        .checkAuth()
        .pipe(
          take(1),
          switchMap((result) => result.isAuthenticated ? this.requireCompletedMfa().pipe(
            switchMap((completed) => completed ? this.exchangeBffSession() : of(void 0)),
          ) : of(void 0)),
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
    if (safeReturnUrl) sessionStorage.setItem(RETURN_URL_KEY, safeReturnUrl);
    else sessionStorage.removeItem(RETURN_URL_KEY);
    if (this.bffOnly) {
      this.markSsoLoginInProgress();
      const authorityOrigin = new URL(this.sessionStatusUrl, window.location.origin).origin;
      const target = safeReturnUrl ?? this.defaultReturnUrl;
      // Return through the SPA origin so Identity redirects back to the Angular app,
      // not to a non-existent /auth/login route on the Identity host.
      const callbackRoute = `${window.location.origin}${this.loginRoute}?returnUrl=${encodeURIComponent(target)}`;
      const loginUrl = new URL(`${authorityOrigin}/Account/Login`);
      loginUrl.searchParams.set('returnUrl', callbackRoute);
      loginUrl.searchParams.set('spaOrigin', window.location.origin);
      window.location.assign(loginUrl.toString());
      return;
    }
    this.oidcLogin();
  }

  oidcLogin(): void {
    if (this.bffOnly) {
      this.login();
      return;
    }
    this.markSsoLoginInProgress();
    this.oidcSecurityService.authorize();
  }

  logout(): void {
    this.markSsoLogout();
    this.broadcastLogout();
    sessionStorage.removeItem(RETURN_URL_KEY);
    this.http.post(this.logoutUrl, {}, { withCredentials: true }).pipe(
      catchError(() => of(void 0)),
      finalize(() => {
        if (this.bffOnly) {
          this.lastAuthenticated = false;
          this.authenticatedSubject.next(false);
          this.router.navigate([this.loginRoute]);
        } else {
          this.oidcSecurityService.logoff().subscribe();
        }
      }),
    ).subscribe();
  }

  trySsoLogin(returnUrl?: string): Observable<boolean> {
    // In BFF mode the Identity cookie is established by the full-document
    // redirect. The SPA returns to /auth/login while the in-progress marker
    // is still present, so that marker must not suppress the cookie exchange.
    // Token-based OIDC flows retain the suppression guard to avoid loops.
    // BFF login must keep polling after a full-document redirect. A transient
    // local logout marker can be written while the SPA is bootstrapping; if it
    // suppressed BFF polling, an authenticated Identity cookie would never be
    // exchanged into the local hishop session. Suppression remains relevant
    // only to token-based OIDC flows to prevent redirect loops.
    if (!this.bffOnly && (this.isSsoLoginInProgress() || this.isSsoSuppressed())) {
      return of(false);
    }

    return this.getIdentitySessionStatus().pipe(
      take(1),
      switchMap((status) => {
        if (!status.authenticated) return of(false);

        // The browser can arrive at the SPA login route after the Identity
        // provider has already completed passkey/MFA. In BFF mode, reuse that
        // authenticated Identity cookie and establish the local BFF session;
        // restarting /Account/Login here creates a redirect loop.
        if (this.bffOnly) {
          return this.exchangeBffSession().pipe(
            map(() => true),
            tap(() => {
              const target = this.safeReturnUrl(returnUrl)
                ?? this.safeReturnUrl(sessionStorage.getItem(RETURN_URL_KEY))
                ?? this.safeReturnUrl(this.defaultReturnUrl) ?? '/';
              this.lastAuthenticated = true;
              this.authenticatedSubject.next(true);
              this.clearSsoLoginInProgress();
              sessionStorage.removeItem(RETURN_URL_KEY);
              this.router.navigateByUrl(target);
            }),
          );
        }

        this.login(returnUrl);
        return of(true);
      }),
      catchError(() => of(false)),
    );
  }

  getAccessToken(): Observable<string> {
    if (this.bffOnly) return of('');
    return this.oidcSecurityService.getAccessToken();
  }

  /**
   * Refreshes the OIDC access token once for all concurrent 401 responses.
   * The shared interceptor uses this seam so every web app follows the same
   * recovery policy without issuing a refresh storm.
   */
  refreshAccessToken(): Observable<boolean> {
    if (this.bffOnly) {
      return this.exchangeBffSession().pipe(
        map(() => true),
        catchError(() => of(false)),
      );
    }
    if (!this.refreshAccessTokenInFlight$) {
      this.refreshAccessTokenInFlight$ = this.oidcSecurityService.forceRefreshSession().pipe(
        map(({ isAuthenticated }) => isAuthenticated),
        catchError(() => of(false)),
        tap((refreshed) => {
          if (!refreshed) this.forceLocalLogout();
        }),
        finalize(() => {
          this.refreshAccessTokenInFlight$ = undefined;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }

    return this.refreshAccessTokenInFlight$;
  }

  /** Ends the local session when the refresh token/session is no longer valid. */
  handleSessionExpired(): void {
    this.forceLocalLogout();
  }

  handleCallback(): Observable<boolean> {
    if (this.bffOnly) {
      return this.getIdentitySessionStatus().pipe(
        switchMap((status) => status.authenticated
          ? this.exchangeBffSession().pipe(map(() => true))
          : of(false)),
        tap((authenticated) => {
          this.lastAuthenticated = authenticated;
          this.authenticatedSubject.next(authenticated);
          this.clearSsoLoginInProgress();
          if (authenticated) {
            const returnUrl = this.safeReturnUrl(sessionStorage.getItem(RETURN_URL_KEY))
              ?? this.safeReturnUrl(this.defaultReturnUrl) ?? '/';
            sessionStorage.removeItem(RETURN_URL_KEY);
            this.router.navigateByUrl(returnUrl);
          } else {
            this.router.navigate([this.loginRoute]);
          }
        }),
        catchError(() => of(false)),
      );
    }
    return this.oidcSecurityService.checkAuth().pipe(
      switchMap(({ isAuthenticated }) => isAuthenticated
        ? this.requireCompletedMfa().pipe(
          switchMap((completed) => completed
            ? this.exchangeBffSession().pipe(map(() => true))
            : of(false)),
        )
        : of(false)),
      tap((isAuthenticated) => {
        this.clearSsoLoginInProgress();
        if (isAuthenticated) {
          const returnUrl = this.safeReturnUrl(sessionStorage.getItem(RETURN_URL_KEY)) ?? this.safeReturnUrl(this.defaultReturnUrl) ?? '/';
          sessionStorage.removeItem(RETURN_URL_KEY);
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
    // Several bootstrap paths can observe the Identity cookie at the same
    // time (initial checkAuth, callback handling and the route guard). A
    // session exchange rotates hishop_sid, so concurrent exchanges can race:
    // the later request deletes the first session while its Set-Cookie can
    // arrive first, leaving the browser with a stale cookie and downstream
    // APIs returning 401. Share one in-flight exchange across all callers.
    if (!this.exchangeBffSessionInFlight$) {
      const body = this.bffClientId ? { clientId: this.bffClientId } : {};
      this.exchangeBffSessionInFlight$ = this.http.post<void>(this.sessionExchangeUrl, body, { withCredentials: true }).pipe(
        map(() => void 0),
        catchError((error: unknown) => {
          // A restarted Identity service can invalidate development tokens (or
          // a revoked session can reach this path). Never leave the UI marked
          // authenticated while every API request is guaranteed to return 401.
          if (error instanceof HttpErrorResponse && (error.status === 401 || error.status === 403)) {
            // A 403 during exchange means the authenticated Identity account
            // is not allowed to use this portal client (for example, a HQ or
            // tech account opening the manufacturing console). Clear the
            // server cookie as well as local state; otherwise login polling
            // keeps replaying the same forbidden exchange every few seconds.
            this.logout();
          }
          // Preserve the failure so the surrounding auth flow publishes
          // `false`; swallowing a 401 here made the caller continue with
          // `map(() => true)` and left the UI authenticated while every API
          // request was rejected.
          return throwError(() => error);
        }),
        finalize(() => {
          this.exchangeBffSessionInFlight$ = undefined;
        }),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    }

    return this.exchangeBffSessionInFlight$;
  }

  private requireCompletedMfa(): Observable<boolean> {
    return this.http.get<IdentityMfaStatus>(this.mfaStatusUrl).pipe(
      map((status) => {
        if (!status.requiresMfa) return true;
        this.redirectToMfaChallenge();
        return false;
      }),
      catchError(() => {
        // A token that cannot prove its MFA state must not be promoted to a
        // BFF session. The user can retry through the normal OIDC flow.
        this.forceLocalLogout();
        return of(false);
      }),
    );
  }

  private forceLocalLogout(): void {
    this.markSsoLogout();
    this.broadcastLogout();
    sessionStorage.removeItem(RETURN_URL_KEY);
    if (!this.bffOnly) this.oidcSecurityService.logoffLocal();
    this.lastAuthenticated = false;
    this.authenticatedSubject.next(false);
    if (!this.router.url.includes(this.loginRoute)) {
      this.router.navigate([this.loginRoute]);
    }
  }

  private redirectToMfaChallenge(): void {
    // Keep the server identity cookie so /connect/authorize can resolve the
    // pending account and redirect to its shared /Account/Mfa page. A local
    // logout alone leaves Angular on /auth/login with no visible MFA step.
    this.markSsoLogout();
    this.lastAuthenticated = false;
    this.authenticatedSubject.next(false);
    this.oidcSecurityService.authorize();
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
