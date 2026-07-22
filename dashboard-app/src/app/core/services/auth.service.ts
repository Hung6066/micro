import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: {
    id: string;
    username: string;
    email: string;
    firstName: string;
    lastName: string;
    fullName: string;
    roles: string[];
  };
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly loginUrl = `${environment.identityUrl}/api/v1/auth/login`;
  private authenticatedSubject = new BehaviorSubject<boolean | null>(null);

  readonly isAuthenticated$: Observable<boolean | null> = this.authenticatedSubject.asObservable();

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {
    this.authenticatedSubject.next(this.hasToken());
  }

  login(returnUrl?: string): void {
    this.router.navigate(['/auth/login'], { queryParams: returnUrl ? { returnUrl } : undefined });
  }

  loginWithCredentials(username: string, password: string): Observable<void> {
    return this.http.post<TokenResponse>(this.loginUrl, { username, password }).pipe(
      tap(response => {
        localStorage.setItem('access_token', response.accessToken);
        localStorage.setItem('refresh_token', response.refreshToken);
        this.authenticatedSubject.next(true);
      }),
      map(() => undefined),
      catchError(err => {
        this.authenticatedSubject.next(false);
        return throwError(() => err);
      }),
    );
  }

  logout(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this.authenticatedSubject.next(false);
    this.router.navigate(['/auth/login']);
  }

  getToken(): string | null {
    return localStorage.getItem('access_token');
  }

  private hasToken(): boolean {
    const token = this.getToken();
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }
}
