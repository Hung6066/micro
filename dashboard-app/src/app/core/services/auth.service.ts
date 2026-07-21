import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly authUrl = `${environment.identityUrl}/auth`;
  private authenticatedSubject = new BehaviorSubject<boolean>(this.hasToken());

  readonly isAuthenticated$: Observable<boolean> = this.authenticatedSubject.asObservable();

  constructor(private readonly http: HttpClient) {}

  login(returnUrl?: string): void {
    const redirectUri = returnUrl
      ? `${window.location.origin}/auth/callback?returnUrl=${encodeURIComponent(returnUrl)}`
      : `${window.location.origin}/auth/callback`;
    window.location.href = `${this.authUrl}/login?redirectUri=${encodeURIComponent(redirectUri)}`;
  }

  logout(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this.authenticatedSubject.next(false);
    window.location.href = `${this.authUrl}/logout?redirectUri=${encodeURIComponent(window.location.origin)}`;
  }

  handleCallback(): Observable<void> {
    return this.http.get<{ accessToken: string; refreshToken?: string }>(`${this.authUrl}/token`, {
      params: { code: this.getCodeFromUrl() },
    }).pipe(
      map(response => {
        localStorage.setItem('access_token', response.accessToken);
        if (response.refreshToken) {
          localStorage.setItem('refresh_token', response.refreshToken);
        }
        this.authenticatedSubject.next(true);
      })
    );
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

  private getCodeFromUrl(): string {
    const params = new URLSearchParams(window.location.search);
    return params.get('code') ?? '';
  }
}
