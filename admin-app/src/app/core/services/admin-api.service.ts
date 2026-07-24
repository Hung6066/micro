import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface OidcClient {
  id?: string;
  clientId: string;
  displayName: string;
  clientType: string;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  scopes?: string[];
  grantTypes?: string[];
}

export interface User {
  id: string;
  userName: string;
  email: string;
  roles: string[];
  isActive: boolean;
}

export interface Role {
  id?: string;
  name: string;
  description?: string;
}

export interface Consent {
  id: string;
  subject: string;
  clientId: string;
  scopes: string[];
  created: string;
}

export interface DashboardStats {
  totalClients: number;
  totalUsers: number;
  totalRoles: number;
  totalConsents: number;
}

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getClients(): Observable<OidcClient[]> {
    return this.http.get<OidcClient[]>(`${this.baseUrl}/clients`);
  }

  getClient(id: string): Observable<OidcClient> {
    return this.http.get<OidcClient>(`${this.baseUrl}/clients/${id}`);
  }

  createClient(client: Partial<OidcClient>): Observable<OidcClient> {
    return this.http.post<OidcClient>(`${this.baseUrl}/clients`, client);
  }

  updateClient(id: string, client: Partial<OidcClient>): Observable<OidcClient> {
    return this.http.put<OidcClient>(`${this.baseUrl}/clients/${id}`, client);
  }

  deleteClient(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/clients/${id}`);
  }

  getUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.baseUrl}/users`);
  }

  getRoles(): Observable<Role[]> {
    return this.http.get<Role[]>(`${this.baseUrl}/roles`);
  }

  createRole(role: Partial<Role>): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/roles`, role);
  }

  getConsents(): Observable<Consent[]> {
    return this.http.get<Consent[]>(`${this.baseUrl}/consents`);
  }

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.baseUrl}/dashboard`);
  }
}
