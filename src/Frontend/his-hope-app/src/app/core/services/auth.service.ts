import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginRequest, RegisterRequest, TokenResponse, User } from '@core/models/auth.model';
import { environment } from '@env/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly baseUrl = `${environment.apiUrl}/auth`;

  constructor(private http: HttpClient) {}

  login(request: LoginRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.baseUrl}/login`, request).pipe(
      tap((res) => this.setSession(res)),
    );
  }

  register(request: RegisterRequest): Observable<TokenResponse> {
    return this.http.post<TokenResponse>(`${this.baseUrl}/register`, request).pipe(
      tap((res) => this.setSession(res)),
    );
  }

  refreshToken(refreshToken: string): Observable<TokenResponse> {
    const accessToken = localStorage.getItem('access_token')!;
    return this.http
      .post<TokenResponse>(`${this.baseUrl}/refresh`, { accessToken, refreshToken })
      .pipe(tap((res) => this.setSession(res)));
  }

  logout(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
  }

  getCurrentUser(): User | null {
    const user = localStorage.getItem('current_user');
    return user ? JSON.parse(user) : null;
  }

  isLoggedIn(): boolean {
    return !!localStorage.getItem('access_token');
  }

  private setSession(res: TokenResponse): void {
    localStorage.setItem('access_token', res.accessToken);
    localStorage.setItem('refresh_token', res.refreshToken);
    localStorage.setItem('current_user', JSON.stringify(res.user));
  }
}
