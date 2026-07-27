import { inject, Injectable } from "@angular/core";
import { Router } from "@angular/router";
import { HttpClient, HttpErrorResponse } from "@angular/common/http";
import {
  BehaviorSubject,
  EMPTY,
  Observable,
  of,
  throwError,
  ReplaySubject,
  timer,
} from "rxjs";
import {
  catchError,
  exhaustMap,
  map,
  retry,
  shareReplay,
  switchMap,
  tap,
  distinctUntilChanged,
  take,
} from "rxjs/operators";
import { OidcSecurityService } from "angular-auth-oidc-client";
import { HisHopeAuthCoordinator } from "@his-hope/frontend-foundation";
import {
  LoginRequest,
  LoginResponse,
  RegisterRequest,
  TokenResponse,
  User,
} from "@core/models/auth.model";
import { environment } from "@env/environment";

@Injectable({ providedIn: "root" })
export class AuthService {
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private currentUserSubject = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();

  private http = inject(HttpClient);
  private oidcSecurityService = inject(OidcSecurityService);
  private router = inject(Router);
  private authCoordinator!: HisHopeAuthCoordinator;
  private static readonly AUTH_CHANNEL = "hishop_auth";
  private static readonly SSO_LOGIN_IN_PROGRESS_KEY = "hishop_sso_login_in_progress_until";
  private currentUserLoad$?: Observable<User | null>;
  private readonly checkAuthInit$ = new ReplaySubject<void>(1);
  private readonly sessionStatusUrl = `${environment.apiUrl}/auth/session-status`;
  private lastAuthenticated = false;
  private ssoLoginInProgress = false;

  constructor() {
    this.authCoordinator = new HisHopeAuthCoordinator(
      this.oidcSecurityService,
      this.http,
      this.router,
      {
        defaultReturnUrl: "/dashboard",
        sessionStatusUrl: this.sessionStatusUrl,
        sessionExchangeUrl: `${environment.apiUrl}/auth/session/exchange`,
        logoutUrl: `${environment.apiUrl}/auth/logout`,
      },
    );

    this.oidcSecurityService.isAuthenticated$.subscribe(({ isAuthenticated }) => {
      this.lastAuthenticated = isAuthenticated;
    });

    this.oidcSecurityService
      .checkAuth()
      .pipe(take(1))
      .subscribe({
        next: ({ isAuthenticated }) => {
          this.lastAuthenticated = isAuthenticated;
          if (isAuthenticated) {
            this.loadUserFromOidc()
              .pipe(switchMap(() => this.ensureBffSession()), take(1))
              .subscribe({ error: () => {} });
          }
          this.checkAuthInit$.next();
          this.checkAuthInit$.complete();
        },
        error: () => {
          this.checkAuthInit$.next();
          this.checkAuthInit$.complete();
        },
      });

    this.initBroadcastChannel();
    this.startSsoSessionMonitor();
  }

  /** Wait for initial OIDC checkAuth to complete (used by guards) */
  checkAuth(): Observable<void> {
    return this.checkAuthInit$.asObservable();
  }

  /** @deprecated Use oidcLogin() for OIDC-based authentication */
  login(request: LoginRequest): Observable<User> {
    return this.http
      .post<LoginResponse>(`${this.baseUrl}/login`, request, { withCredentials: true })
      .pipe(
        switchMap(() => {
          return this.http.get<User>(`${this.baseUrl}/me`, { withCredentials: true });
        }),
        tap((user) => {
          this.currentUserSubject.next(user);
        }),
        catchError(this.handleError),
      );
  }

  /** @deprecated Use OIDC flows for registration */
  register(request: RegisterRequest): Observable<User> {
    return this.http
      .post<User>(`${this.baseUrl}/register`, request, {
        withCredentials: true,
      })
      .pipe(
        tap((user) => this.currentUserSubject.next(user)),
        catchError(this.handleError),
      );
  }

  /** @deprecated OIDC handles token refresh automatically */
  refreshToken(): Observable<User> {
    return this.http
      .post<TokenResponse>(
        `${this.baseUrl}/refresh`,
        {},
        { withCredentials: true },
      )
      .pipe(
        tap((response) => {
          this.currentUserSubject.next(response.user);
        }),
        map((response) => response.user),
        retry(1),
        catchError(this.handleError),
      );
  }

  /** @deprecated Use oidcLogout() for OIDC-based logout */
  logout(): Observable<void> {
    this.markSsoLogout();

    return this.http
      .post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true })
      .pipe(
        tap(() => {
          this.currentUserSubject.next(null);
          this.clearStoredAccessToken();
          this.permissionCache.clear();
          this.redirectToCentralLogout();
        }),
        retry(1),
        catchError((error) => {
          this.currentUserSubject.next(null);
          this.oidcSecurityService.logoffLocal();
          this.clearStoredAccessToken();
          this.permissionCache.clear();
          this.redirectToCentralLogout();
          return of(void 0);
        }),
      );
  }

  // ─── OIDC Methods ─────────────────────────────────────────────────

  oidcLogin(returnUrl?: string): void {
    this.authCoordinator.login(returnUrl);
  }

  oidcLogout(): void {
    this.markSsoLogout();
    this.broadcastLogout();
    this.authCoordinator.logout();
    this.currentUserSubject.next(null);
    this.permissionCache.clear();
  }

  trySsoLogin(returnUrl?: string): Observable<boolean> {
    return this.authCoordinator.trySsoLogin(returnUrl);
  }

  handleCallback(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      switchMap(({ isAuthenticated }) => {
        if (!isAuthenticated) {
          this.markSsoLogout();
          this.ssoLoginInProgress = false;
          return of(false);
        }

        return this.loadUserFromOidc().pipe(
          switchMap(() => this.ensureBffSession()),
          map(() => true),
          catchError(() => of(true)),
        );
      }),
    );
  }

  completeSsoLogin(): void {
    sessionStorage.removeItem(AuthService.SSO_LOGIN_IN_PROGRESS_KEY);
    this.ssoLoginInProgress = false;
  }

  isAuthenticated(): Observable<boolean> {
    return this.oidcSecurityService.isAuthenticated$.pipe(
      map(({ isAuthenticated }) => isAuthenticated),
    );
  }

  getAccessToken(): Observable<string> {
    return this.authCoordinator.getAccessToken();
  }

  getUserData(): Observable<any> {
    return this.oidcSecurityService.userData$;
  }

  // ─── Backward-compatible Public API ────────────────────────────────

  getCurrentUser(): Observable<User> {
    const currentUser = this.currentUserSubject.value;
    if (currentUser) {
      return of(currentUser);
    }

    return this.http
      .get<User>(`${this.baseUrl}/me`, { withCredentials: true })
      .pipe(
        tap((user) => this.currentUserSubject.next(user)),
        shareReplay(1),
        retry(1),
        catchError(this.handleError),
      );
  }

  isLoggedIn(): Observable<boolean> {
    if (this.currentUserSubject.value) {
      return of(true);
    }
    return this.isAuthenticated();
  }

  ensureCurrentUser(): Observable<User | null> {
    const currentUser = this.currentUserSubject.value;
    if (currentUser) {
      return of(currentUser);
    }
    return this.loadUserFromOidc();
  }

  /** Retrieve the stored access token from OIDC */
  getStoredAccessToken(): string | null {
    let token: string | null = null;
    this.oidcSecurityService
      .getAccessToken()
      .pipe(take(1))
      .subscribe((t) => (token = t));
    return token;
  }

  /** No-op: OIDC manages tokens */
  storeAccessToken(_token: string): void {}

  /** No-op: OIDC manages tokens */
  clearStoredAccessToken(): void {}

  // ─── Role Methods ─────────────────────────────────────────────────

  getUserRoles(): string[] {
    const user = this.currentUserSubject.value;
    return user?.roles ?? [];
  }

  getUserPermissions(): string[] {
    const user = this.currentUserSubject.value;
    if (user?.permissions && user.permissions.length > 0) {
      return user.permissions;
    }
    return [];
  }

  hasRole(role: string | string[]): boolean {
    const userRoles = this.getUserRoles();
    if (typeof role === "string") {
      return userRoles.includes(role);
    }
    return role.some((r) => userRoles.includes(r));
  }

  hasPermission(permission: string | string[]): boolean {
    const userPermissions = this.getUserPermissions();
    if (typeof permission === "string") {
      return userPermissions.includes(permission);
    }
    return permission.every((p) => userPermissions.includes(p));
  }

  getCurrentUserRoles(): Observable<string[]> {
    return this.currentUser$.pipe(
      map((user) => user?.roles ?? []),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
    );
  }

  // ─── API-based Permission Check ─────────────────────────────────────

  private permissionCache = new Map<
    string,
    { granted: boolean; timestamp: number }
  >();
  private readonly PERMISSION_CACHE_TTL = 5 * 60 * 1000;

  hasPermissionOnServer(permission: string): Observable<boolean> {
    const cached = this.permissionCache.get(permission);
    if (
      cached !== undefined &&
      Date.now() - cached.timestamp < this.PERMISSION_CACHE_TTL
    ) {
      return of(cached.granted);
    }

    return this.http
      .post<{
        granted: boolean;
      }>(`${this.baseUrl}/check-permission`, { permission }, { withCredentials: true })
      .pipe(
        map((res) => res.granted),
        tap((granted) => {
          this.permissionCache.set(permission, {
            granted,
            timestamp: Date.now(),
          });
        }),
        catchError(() => of(false)),
      );
  }

  // ─── Private ───────────────────────────────────────────────────────

  private loadUserFromOidc(): Observable<User | null> {
    if (this.currentUserLoad$) return this.currentUserLoad$;

    this.currentUserLoad$ = this.http
      .get<User>(`${this.baseUrl}/me`, { withCredentials: true })
      .pipe(
        tap((user) => this.currentUserSubject.next(user)),
        shareReplay(1),
        retry(1),
        catchError((err) => {
          this.currentUserLoad$ = undefined;
          return this.handleError(err);
        }),
      );

    return this.currentUserLoad$;
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    if (!environment.production) {
      let errorMessage = "An unknown error occurred";
      if (error.error instanceof ErrorEvent) {
        errorMessage = `Client error: ${error.error.message}`;
      } else {
        errorMessage = `Server error: ${error.status} - ${error.message}`;
      }
      console.error("[AuthService]", errorMessage);
    }
    return throwError(() => error);
  }

  private initBroadcastChannel(): void {
    try {
      const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
      channel.onmessage = (event: MessageEvent) => {
        if (event.data?.type === "LOGOUT") {
          this.currentUserSubject.next(null);
          this.oidcSecurityService.logoffLocal();
          this.permissionCache.clear();
          if (!this.router.url.includes("/auth/login")) {
            this.router.navigate(["/auth/login"]);
          }
        }
      };
    } catch {
      // BroadcastChannel not supported (Safari < 15.4) — silently ignore
    }
  }

  private broadcastLogout(): void {
    try {
      const channel = new BroadcastChannel(AuthService.AUTH_CHANNEL);
      channel.postMessage({ type: "LOGOUT" });
      channel.close();
    } catch {
      // BroadcastChannel not supported
    }
  }

  private ensureBffSession(): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/session/exchange`, {}, { withCredentials: true })
      .pipe(
        map(() => void 0),
        catchError(() => of(void 0)),
      );
  }

  private startSsoSessionMonitor(): void {
    timer(2000, 2000).pipe(
      exhaustMap(() => {
        if (!this.lastAuthenticated) {
          return EMPTY;
        }
        return this.getIdentitySessionStatus().pipe(catchError(() => EMPTY));
      }),
    ).subscribe((status) => {
      if (!status.authenticated) {
        this.forceLocalLogout();
      }
    });
  }

  private getIdentitySessionStatus(): Observable<{ authenticated: boolean; userName?: string }> {
    return this.http.get<{ authenticated: boolean; userName?: string }>(
      this.sessionStatusUrl,
      { withCredentials: true },
    );
  }

  private forceLocalLogout(): void {
    this.markSsoLogout();
    this.broadcastLogout();
    this.currentUserSubject.next(null);
    this.oidcSecurityService.logoffLocal();
    this.permissionCache.clear();
    this.currentUserLoad$ = undefined;
    this.lastAuthenticated = false;
    if (!this.router.url.includes("/auth/login")) {
      this.router.navigate(["/auth/login"]);
    }
  }

  private markSsoLogout(): void {
    sessionStorage.setItem("hishop_sso_suppressed_until", String(Date.now() + 10000));
  }

  private isSsoSuppressed(): boolean {
    const until = Number(sessionStorage.getItem("hishop_sso_suppressed_until") ?? 0);
    return Date.now() < until;
  }

  private markSsoLoginInProgress(): void {
    this.ssoLoginInProgress = true;
    sessionStorage.setItem(AuthService.SSO_LOGIN_IN_PROGRESS_KEY, String(Date.now() + 120000));
  }

  private isSsoLoginInProgress(): boolean {
    const until = Number(sessionStorage.getItem(AuthService.SSO_LOGIN_IN_PROGRESS_KEY) ?? 0);
    return this.ssoLoginInProgress || Date.now() < until;
  }

  private redirectToCentralLogout(): void {
    const postLogoutRedirectUri = `${window.location.origin}/auth/login`;
    const logoutUrl = `${window.location.origin}/connect/logout?post_logout_redirect_uri=${encodeURIComponent(postLogoutRedirectUri)}`;
    window.location.assign(logoutUrl);
  }
}
