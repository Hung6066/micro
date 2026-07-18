import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { catchError, map, retry, shareReplay, tap, distinctUntilChanged } from 'rxjs/operators';
import { LoginRequest, RegisterRequest, User } from '@core/models/auth.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  private currentUserSubject = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSubject.asObservable();

  private http = inject(HttpClient);

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
      .post<User>(`${this.baseUrl}/refresh`, {}, { withCredentials: true })
      .pipe(
        tap((user) => this.currentUserSubject.next(user)),
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
    return this.http.get<User>(`${this.baseUrl}/me`, { withCredentials: true }).pipe(
      tap((user) => this.currentUserSubject.next(user)),
      shareReplay(1),
      retry(1),
      catchError(this.handleError),
    );
  }

  isLoggedIn(): Observable<boolean> {
    return this.http.get<{ authenticated: boolean }>(`${this.baseUrl}/verify`, { withCredentials: true }).pipe(
      map((res) => res.authenticated),
      retry(1),
      catchError(() => of(false)),
    );
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
    // Fallback: try to decode JWT from sessionStorage
    const token = this.getStoredAccessToken();
    if (token) {
      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        return payload.permissions ?? payload.roles ?? [];
      } catch {
        return [];
      }
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
  // JWT access token chỉ lưu trong RAM, không persist vào sessionStorage.
  // Reload tab → mất token → refresh qua HttpOnly cookie hoặc redirect login.
  // Security > convenience: không cho phép XSS đọc token từ storage.

  private accessToken: string | null = null;

  /** Store the JWT access token in memory only */
  storeAccessToken(token: string): void {
    this.accessToken = token;
  }

  /** Retrieve the stored JWT access token from memory */
  getStoredAccessToken(): string | null {
    return this.accessToken;
  }

  /** Remove the stored JWT access token from memory */
  clearStoredAccessToken(): void {
    this.accessToken = null;
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
}
