import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { delay, map, distinctUntilChanged } from 'rxjs/operators';
import { LoginRequest, RegisterRequest, User } from '@core/models/auth.model';
import { mockUsers, mockRoles } from './mock-data';

/**
 * Maps role names to their permissions from mock role definitions.
 */
function getPermissionsForRoles(roles: string[]): string[] {
  const perms = new Set<string>();
  for (const role of roles) {
    const found = mockRoles.find((r) => r.name === role);
    if (found) {
      found.permissions.forEach((p) => perms.add(p));
    }
  }
  return Array.from(perms);
}

/** Full mock permissions for the admin user (usr-001) */
const adminPermissions = getPermissionsForRoles(mockUsers[0].roles);

@Injectable({ providedIn: 'root' })
export class MockAuthService {
  private currentUserSubject = new BehaviorSubject<User | null>({
    ...mockUsers[0],
    permissions: adminPermissions,
  });
  currentUser$ = this.currentUserSubject.asObservable();

  private delayMs(): number {
    return 300 + Math.floor(Math.random() * 200);
  }

  login(request: LoginRequest): Observable<User> {
    // Always succeed — mock returns the admin user
    const user = { ...mockUsers[0], permissions: adminPermissions };
    this.currentUserSubject.next(user);
    return of(user).pipe(delay(this.delayMs()));
  }

  register(request: RegisterRequest): Observable<User> {
    const newUser: User = {
      id: `usr-${String(mockUsers.length + 1).padStart(3, '0')}`,
      username: request.username,
      email: request.email,
      firstName: request.firstName,
      lastName: request.lastName,
      middleName: request.middleName,
      fullName: `${request.lastName} ${request.middleName ? request.middleName + ' ' : ''}${request.firstName}`,
      licenseNumber: request.licenseNumber,
      specialty: request.specialty,
      roles: ['doctor'],
      permissions: getPermissionsForRoles(['doctor']),
    };
    this.currentUserSubject.next(newUser);
    return of(newUser).pipe(delay(this.delayMs()));
  }

  refreshToken(): Observable<User> {
    const user = this.currentUserSubject.value || { ...mockUsers[0], permissions: adminPermissions };
    return of(user).pipe(delay(this.delayMs()));
  }

  logout(): Observable<void> {
    this.currentUserSubject.next(null);
    this.clearStoredAccessToken();
    return of(undefined).pipe(delay(this.delayMs()));
  }

  getCurrentUser(): Observable<User> {
    const user = this.currentUserSubject.value || { ...mockUsers[0], permissions: adminPermissions };
    return of(user).pipe(delay(this.delayMs()));
  }

  isLoggedIn(): Observable<boolean> {
    return of(true).pipe(delay(this.delayMs()));
  }

  // ─── Role & Permission Methods ──────────────────────────────────────

  getUserRoles(): string[] {
    return this.currentUserSubject.value?.roles ?? [];
  }

  getUserPermissions(): string[] {
    const user = this.currentUserSubject.value;
    if (user?.permissions && user.permissions.length > 0) {
      return user.permissions;
    }
    return getPermissionsForRoles(this.getUserRoles());
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

  // ─── Token Storage (Mock) ──────────────────────────────────────────

  storeAccessToken(token: string): void {
    try {
      sessionStorage.setItem('hishope_access_token', token);
    } catch {
      // noop
    }
  }

  getStoredAccessToken(): string | null {
    try {
      return sessionStorage.getItem('hishope_access_token');
    } catch {
      return null;
    }
  }

  clearStoredAccessToken(): void {
    try {
      sessionStorage.removeItem('hishope_access_token');
    } catch {
      // noop
    }
  }
}
