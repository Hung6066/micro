import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError, ReplaySubject } from 'rxjs';
import { catchError, map, retry, shareReplay, tap, distinctUntilChanged, take } from 'rxjs/operators';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { LoginRequest, RegisterRequest, TokenResponse, User } from '@core/models/auth.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private currentUserSubject = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();

  private http = inject(HttpClient);
  private oidcSecurityService = inject(OidcSecurityService);
  private currentUserLoad$?: Observable<User | null>;
  private readonly checkAuthInit$ = new ReplaySubject<void>(1);

  constructor() {
    this.oidcSecurityService.checkAuth().pipe(take(1)).subscribe({
      next: ({ isAuthenticated }) => {
        if (isAuthenticated) this.loadUserFromOidc();
        this.checkAuthInit$.next();
        this.checkAuthInit$.complete();
      },
      error: () => {
        this.checkAuthInit$.next();
        this.checkAuthInit$.complete();
      },
    });
  }

  /** Wait for initial OIDC checkAuth to complete (used by guards) */
  checkAuth(): Observable<void> {
    return this.checkAuthInit$.asObservable();
  }

  /** @deprecated Use oidcLogin() for OIDC-based authentication */
  login(request: LoginRequest): Observable<User> {
    return this.http.post<any>(`${this.baseUrl}/login`, request, { withCredentials: true }).pipe(
      tap((response) => {
        if (response.accessToken) {
          this.storeAccessToken(response.accessToken);
        }
        if (response.user) {
          this.currentUserSubject.next(response.user);
        }
      }),
      map((response) => response.user as User),
      catchError(this.handleError),
    );
  }

  /** @deprecated Use OIDC flows for registration */
  register(request: RegisterRequest): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/register`, request, { withCredentials: true }).pipe(
      tap((user) => this.currentUserSubject.next(user)),
      catchError(this.handleError),
    );
  }

  /** @deprecated OIDC handles token refresh automatically */
  refreshToken(): Observable<User> {
    return this.http
      .post<TokenResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true })
      .pipe(
        tap((response) => {
          if (response.accessToken) {
            this.storeAccessToken(response.accessToken);
          }
          this.currentUserSubject.next(response.user);
        }),
        map((response) => response.user),
        retry(1),
        catchError(this.handleError),
      );
  }

  /** @deprecated Use oidcLogout() for OIDC-based logout */
  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.currentUserSubject.next(null);
        this.clearStoredAccessToken();
        this.permissionCache.clear();
      }),
      retry(1),
      catchError((error) => {
        this.currentUserSubject.next(null);
        this.clearStoredAccessToken();
        this.permissionCache.clear();
        return this.handleError(error);
      }),
    );
  }

  // ─── OIDC Methods ─────────────────────────────────────────────────

  oidcLogin(returnUrl?: string): void {
    if (returnUrl) sessionStorage.setItem('oidc_returnUrl', returnUrl);
    this.oidcSecurityService.authorize();
  }

  oidcLogout(): void {
    this.oidcSecurityService.logoff().subscribe(() => {
      this.currentUserSubject.next(null);
      this.permissionCache.clear();
    });
  }

  handleCallback(): Observable<boolean> {
    return this.oidcSecurityService.checkAuth().pipe(
      map(({ isAuthenticated }) => isAuthenticated),
      tap((isAuth) => {
        if (isAuth) this.loadUserFromOidc();
      }),
    );
  }

  isAuthenticated(): Observable<boolean> {
    return this.oidcSecurityService.isAuthenticated$.pipe(
      map(({ isAuthenticated }) => isAuthenticated),
    );
  }

  getAccessToken(): Observable<string> {
    return this.oidcSecurityService.getAccessToken();
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

    return this.http.get<User>(`${this.baseUrl}/me`, { withCredentials: true }).pipe(
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
    this.oidcSecurityService.getAccessToken().pipe(take(1)).subscribe(t => token = t);
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
    if (typeof role === 'string') {
      return userRoles.includes(role);
    }
    return role.some((r) => userRoles.includes(r));
  }

  hasPermission(permission: string | string[]): boolean {
    const userPermissions = this.getUserPermissions();
    if (typeof permission === 'string') {
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

  private permissionCache = new Map<string, { granted: boolean; timestamp: number }>();
  private readonly PERMISSION_CACHE_TTL = 5 * 60 * 1000;

  hasPermissionOnServer(permission: string): Observable<boolean> {
    const cached = this.permissionCache.get(permission);
    if (cached !== undefined && Date.now() - cached.timestamp < this.PERMISSION_CACHE_TTL) {
      return of(cached.granted);
    }

    return this.http.post<{ granted: boolean }>(
      `${this.baseUrl}/check-permission`,
      { permission },
      { withCredentials: true },
    ).pipe(
      map((res) => res.granted),
      tap((granted) => {
        this.permissionCache.set(permission, { granted, timestamp: Date.now() });
      }),
      catchError(() => of(false)),
    );
  }

  // ─── Private ───────────────────────────────────────────────────────

  private loadUserFromOidc(): Observable<User | null> {
    if (this.currentUserLoad$) return this.currentUserLoad$;

    this.currentUserLoad$ = this.http.get<User>(`${this.baseUrl}/me`, { withCredentials: true }).pipe(
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
      let errorMessage = 'An unknown error occurred';
      if (error.error instanceof ErrorEvent) {
        errorMessage = `Client error: ${error.error.message}`;
      } else {
        errorMessage = `Server error: ${error.status} - ${error.message}`;
      }
      console.error('[AuthService]', errorMessage);
    }
    return throwError(() => error);
  }
}
