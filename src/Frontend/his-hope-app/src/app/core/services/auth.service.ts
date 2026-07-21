import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { catchError, map, retry, shareReplay, tap, distinctUntilChanged } from 'rxjs/operators';
import { LoginRequest, RegisterRequest, TokenResponse, User } from '@core/models/auth.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${environment.apiUrl}/auth`;
  private readonly accessTokenStorageKey = 'hishope_access_token';

  private currentUserSubject = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();

  private http = inject(HttpClient);
  private currentUserLoad$?: Observable<User | null>;

  constructor() {
    const tokenUser = this.getUserFromStoredAccessToken();
    if (tokenUser) {
      this.currentUserSubject.next(tokenUser);
    }
  }

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

  register(request: RegisterRequest): Observable<User> {
    return this.http.post<User>(`${this.baseUrl}/register`, request, { withCredentials: true }).pipe(
      tap((user) => this.currentUserSubject.next(user)),
      catchError(this.handleError),
    );
  }

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

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.currentUserSubject.next(null);
        this.clearStoredAccessToken();
      }),
      retry(1),
      catchError((error) => {
        // Always clear local state on explicit logout, even if backend fails
        this.currentUserSubject.next(null);
        this.clearStoredAccessToken();
        return this.handleError(error);
      }),
    );
  }

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

    return this.http.get<{ authenticated: boolean }>(`${this.baseUrl}/verify`, { withCredentials: true }).pipe(
      map((res) => res.authenticated),
      retry(1),
      catchError(() => of(false)),
    );
  }

  ensureCurrentUser(): Observable<User | null> {
    const currentUser = this.currentUserSubject.value;
    if (currentUser) {
      return of(currentUser);
    }

    const tokenUser = this.getUserFromStoredAccessToken();
    if (tokenUser) {
      this.currentUserSubject.next(tokenUser);
      return of(tokenUser);
    }

    return of(null);
  }

  // ─── Role Methods ─────────────────────────────────────────────────

  /** Extract roles from the current user object */
  getUserRoles(): string[] {
    const user = this.currentUserSubject.value;
    return user?.roles ?? [];
  }

  /** Extract permissions from the current user object or JWT claims */
  getUserPermissions(): string[] {
    const user = this.currentUserSubject.value;
    if (user?.permissions && user.permissions.length > 0) {
      return user.permissions;
    }
    const tokenUser = this.getUserFromStoredAccessToken();
    if (tokenUser?.permissions?.length) {
      return tokenUser.permissions;
    }
    return [];
  }

  /** Check if user has a specific role */
  hasRole(role: string | string[]): boolean {
    const userRoles = this.getUserRoles();
    if (typeof role === 'string') {
      return userRoles.includes(role);
    }
    return role.some((r) => userRoles.includes(r));
  }

  /** Check if user has a specific permission */
  hasPermission(permission: string | string[]): boolean {
    const userPermissions = this.getUserPermissions();
    if (typeof permission === 'string') {
      return userPermissions.includes(permission);
    }
    return permission.every((p) => userPermissions.includes(p));
  }

  /** Observable of current user roles, emits on change */
  getCurrentUserRoles(): Observable<string[]> {
    return this.currentUser$.pipe(
      map((user) => user?.roles ?? []),
      distinctUntilChanged((a, b) => JSON.stringify(a) === JSON.stringify(b)),
    );
  }

  // ─── Token Storage (Memory-only) ────────────────────────────────────
  // JWT access token được mirror vào sessionStorage để survive hard reload.
  // Memory vẫn là source of truth trong runtime; storage chỉ phục vụ hydrate.

  private accessToken: string | null = null;

  /** Store the JWT access token in memory only */
  storeAccessToken(token: string): void {
    this.accessToken = token;
    this.writeStoredAccessToken(token);
    this.currentUserLoad$ = undefined;
  }

  /** Retrieve the stored JWT access token from memory */
  getStoredAccessToken(): string | null {
    if (this.accessToken) {
      return this.accessToken;
    }

    this.accessToken = this.readStoredAccessToken();
    return this.accessToken;
  }

  /** Remove the stored JWT access token from memory */
  clearStoredAccessToken(): void {
    this.accessToken = null;
    this.writeStoredAccessToken(null);
    this.currentUserLoad$ = undefined;
  }

  // ─── API-based Permission Check ─────────────────────────────────────
  // Thay vì decode JWT client-side, check permission qua backend API.
  // Cache kết quả trong memory với TTL 5 phút (riêng cho mỗi permission).

  private permissionCache = new Map<string, { granted: boolean; timestamp: number }>();
  private readonly PERMISSION_CACHE_TTL = 5 * 60 * 1000;

  /** Check permission via backend API (không decode JWT local) */
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

  private readStoredAccessToken(): string | null {
    try {
      return sessionStorage.getItem(this.accessTokenStorageKey);
    } catch {
      return null;
    }
  }

  private writeStoredAccessToken(token: string | null): void {
    try {
      if (token) {
        sessionStorage.setItem(this.accessTokenStorageKey, token);
      } else {
        sessionStorage.removeItem(this.accessTokenStorageKey);
      }
    } catch {
      // noop
    }
  }

  private getUserFromStoredAccessToken(): User | null {
    const token = this.getStoredAccessToken();
    if (!token) {
      return null;
    }

    try {
      const payload = this.decodeJwtPayload(token);
      if (!payload) {
        return null;
      }

      const roleClaimUri = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
      const roles = Array.isArray(payload['roles'])
        ? payload['roles'] as string[]
        : Array.isArray(payload[roleClaimUri])
          ? payload[roleClaimUri] as string[]
        : typeof payload['role'] === 'string'
          ? [payload['role'] as string]
          : typeof payload[roleClaimUri] === 'string'
            ? [payload[roleClaimUri] as string]
          : [];
      const permissions = Array.isArray(payload['permissions'])
        ? payload['permissions'] as string[]
        : typeof payload['permissions'] === 'string'
          ? (payload['permissions'] as string).split(',').map((permission: string) => permission.trim()).filter(Boolean)
          : [];
      const fullName = typeof payload['fullName'] === 'string'
        ? payload['fullName'] as string
        : typeof payload['unique_name'] === 'string'
          ? payload['unique_name'] as string
          : typeof payload['email'] === 'string'
            ? payload['email'] as string
            : '';
      const nameParts = fullName.trim().split(/\s+/).filter(Boolean);
      const firstName = typeof payload['firstName'] === 'string'
        ? payload['firstName'] as string
        : nameParts.slice(0, -1).join(' ') || fullName;
      const lastName = typeof payload['lastName'] === 'string'
        ? payload['lastName'] as string
        : nameParts.length > 1
          ? nameParts[nameParts.length - 1]
          : '';

      return {
        id: typeof payload['sub'] === 'string' ? payload['sub'] as string : '',
        username: typeof payload['unique_name'] === 'string' ? payload['unique_name'] as string : fullName,
        email: typeof payload['email'] === 'string' ? payload['email'] as string : '',
        firstName,
        lastName,
        fullName,
        roles,
        permissions,
      };
    } catch {
      return null;
    }
  }

  private decodeJwtPayload(token: string): Record<string, unknown> | null {
    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }

    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
    return JSON.parse(new TextDecoder('utf-8').decode(bytes));
  }
}
