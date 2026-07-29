import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, from, timer } from 'rxjs';
import { catchError, filter, map, switchMap, take, takeWhile } from 'rxjs/operators';
import { HisHopeBulkActionRequest, HisHopePageQuery, HisHopePageResult, HisHopeTableExportRequest } from '@his-hope/frontend-foundation';
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
  jwks?: string;
  concurrencyToken?: string;
}

export interface ClientSecretResponse {
  clientId: string;
  clientSecret: string;
  message: string;
  tokenEndpointAuthMethod?: string;
}

export interface ClientOnboarding {
  clientId: string;
  displayName: string;
  issuer: string;
  authorizationEndpoint: string;
  tokenEndpoint: string;
  jwksUri: string;
  grantTypes: string[];
  scopes: string[];
  tokenEndpointAuthMethod: string;
}

export interface User {
  id: string;
  userName: string;
  email: string;
  roles: string[];
  isActive: boolean;
  concurrencyToken?: string;
}

export interface Role {
  id?: string;
  name: string;
  description?: string;
  concurrencyToken?: string;
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

export type AdminPageQuery = HisHopePageQuery;

export interface AdminPageResult<T> extends HisHopePageResult<T> {}

export interface AdminBulkActionResponse {
  actionId: string;
  requested: number;
  updated: number;
}

export interface AdminJobContract {
  jobId: string;
  resource: string;
  actionId: string;
  status: string;
  processed: number;
  total: number;
  downloadUrl?: string;
  errorCode?: string;
}

export interface AdminTableView { name: string; payloadJson: string; updatedAt: string; }
export interface AdminTableAnalysisResult { resource: string; operation: 'aggregate' | 'pivot'; groupBy: string; rows: Array<{ key: string; count: number }>; total: number; }
export interface IdentitySetting { key: string; value: unknown; description?: string; }

@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getClients(): Observable<OidcClient[]> {
    return this.getClientsPage({ page: 1, pageSize: 100 }).pipe(
      map(response => response.items.map(client => ({
        ...client,
        clientType: client.clientType ?? '',
      }))),
    );
  }

  getClientsPage(query: AdminPageQuery): Observable<AdminPageResult<OidcClient>> {
    const params = this.pageParams(query);
    return this.http.get<AdminPageResult<OidcClient & { type?: string }>>(`${this.baseUrl}/clients`, { params }).pipe(
      map(response => ({
        items: response.items.map(client => ({ ...client, clientType: client.clientType ?? client.type ?? '' })),
        totalCount: response.totalCount,
        page: response.page,
        pageSize: response.pageSize,
        totalPages: response.totalPages,
        hasNextPage: response.hasNextPage,
        hasPreviousPage: response.hasPreviousPage,
      })),
    );
  }

  getClient(id: string): Observable<OidcClient> {
    return this.http.get<OidcClient>(`${this.baseUrl}/clients/${id}`);
  }

  createClient(client: Partial<OidcClient>): Observable<ClientSecretResponse> {
    return this.http.post<ClientSecretResponse>(`${this.baseUrl}/clients`, client);
  }

  updateClient(id: string, client: Partial<OidcClient>): Observable<OidcClient> {
    return this.http.put<OidcClient>(`${this.baseUrl}/clients/${id}`, client);
  }

  deleteClient(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/clients/${id}`);
  }

  rotateClientSecret(id: string): Observable<ClientSecretResponse> {
    return this.http.post<ClientSecretResponse>(`${this.baseUrl}/clients/${id}/rotate-secret`, {});
  }

  getClientOnboarding(id: string): Observable<ClientOnboarding> {
    return this.http.get<ClientOnboarding>(`${this.baseUrl}/clients/${id}/onboarding`);
  }

  getUsers(): Observable<User[]> {
    return this.getUsersPage({ page: 1, pageSize: 100 }).pipe(map(response => response.items));
  }

  getUsersPage(query: AdminPageQuery): Observable<AdminPageResult<User>> {
    return this.http.get<AdminPageResult<User>>(`${this.baseUrl}/users`, { params: this.pageParams(query) });
  }

  bulkUsers(request: HisHopeBulkActionRequest): Observable<AdminBulkActionResponse> {
    return this.bulkTable('users', request);
  }

  bulkTable(resource: 'users' | 'roles' | 'clients', request: HisHopeBulkActionRequest): Observable<AdminBulkActionResponse> {
    return this.http.post<AdminBulkActionResponse>(`${this.baseUrl}/tables/${resource}/bulk`, {
      actionId: request.actionId,
      rowKeys: request.rowKeys,
      query: request.query,
      selection: request.selection,
    });
  }

  exportTable(resource: 'users' | 'roles' | 'clients', request: HisHopeTableExportRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/tables/${resource}/export`, {
      format: request.format,
      columns: request.columns,
      rowKeys: request.rowKeys,
      query: request.query,
      async: request.async ?? false,
      maskSensitive: request.maskSensitive ?? true,
    }, { observe: 'response', responseType: 'blob' }).pipe(
      switchMap(response => {
        const contentType = response.headers.get('content-type') ?? '';
        if (!contentType.includes('json')) return of(response.body as Blob);
        return from((response.body as Blob).text()).pipe(
          map(text => JSON.parse(text) as AdminJobContract),
          switchMap(job => this.streamAdminJob(job.jobId).pipe(catchError(() => this.waitForAdminJob(job.jobId)))),
        );
      }),
    );
  }

  getTableViews(resource: 'users' | 'roles' | 'clients'): Observable<AdminTableView[]> {
    return this.http.get<AdminTableView[]>(`${this.baseUrl}/tables/${resource}/views`);
  }

  saveTableView(resource: 'users' | 'roles' | 'clients', name: string, payload: unknown): Observable<AdminTableView> {
    return this.http.put<AdminTableView>(`${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`, { payloadJson: JSON.stringify(payload) });
  }

  deleteTableView(resource: 'users' | 'roles' | 'clients', name: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/tables/${resource}/views/${encodeURIComponent(name)}`);
  }

  analyzeTable(resource: 'users' | 'roles' | 'clients', operation: 'aggregate' | 'pivot' | 'formula', groupBy: string, formulaId?: string, detailLimit = 0): Observable<AdminTableAnalysisResult> {
    return this.http.post<AdminTableAnalysisResult>(`${this.baseUrl}/tables/${resource}/analysis`, { operation, groupBy, metric: 'count', formulaId, detailLimit });
  }

  previewUserImport(file: File): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/users/bulk/preview`, file, { headers: { 'Content-Type': file.type || 'text/csv' } });
  }

  private waitForAdminJob(jobId: string): Observable<Blob> {
    return timer(0, 1000).pipe(
      switchMap(() => this.http.get<AdminJobContract>(`${this.baseUrl}/tables/jobs/${jobId}`)),
      takeWhile(job => job.status !== 'Completed' && job.status !== 'Failed' && job.status !== 'Cancelled', true),
      filter(job => job.status === 'Completed'),
      take(1),
      switchMap(() => this.http.get(`${this.baseUrl}/tables/jobs/${jobId}/download`, { responseType: 'blob' })),
    );
  }

  private streamAdminJob(jobId: string): Observable<Blob> {
    return new Observable<AdminJobContract>(subscriber => {
      const source = new EventSource(`${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/events`, { withCredentials: true });
      source.addEventListener('job', event => {
        try {
          const job = JSON.parse((event as MessageEvent).data) as AdminJobContract;
          subscriber.next(job);
          if (job.status === 'Completed' || job.status === 'Failed' || job.status === 'Cancelled') { source.close(); subscriber.complete(); }
        } catch (error) { source.close(); subscriber.error(error); }
      });
      source.onerror = () => { source.close(); subscriber.error(new Error('SSE job stream unavailable')); };
      return () => source.close();
    }).pipe(
      filter(job => job.status === 'Completed'),
      take(1),
      switchMap(() => this.http.get(`${this.baseUrl}/tables/jobs/${encodeURIComponent(jobId)}/download`, { responseType: 'blob' })),
    );
  }

  getRoles(): Observable<Role[]> {
    return this.getRolesPage({ page: 1, pageSize: 100 }).pipe(map(response => response.items));
  }

  getRolesPage(query: AdminPageQuery): Observable<AdminPageResult<Role>> {
    return this.http.get<AdminPageResult<Role>>(`${this.baseUrl}/roles`, { params: this.pageParams(query) });
  }

  createRole(role: Partial<Role>): Observable<Role> {
    return this.http.post<Role>(`${this.baseUrl}/roles`, role);
  }

  getConsents(): Observable<Consent[]> {
    return this.http.get<AdminPageResult<Consent>>(`${this.baseUrl}/consents`, { params: this.pageParams({ page: 1, pageSize: 100 }) }).pipe(
      map(response => response.items),
    );
  }

  getConsentsPage(query: AdminPageQuery): Observable<AdminPageResult<Consent>> {
    return this.http.get<AdminPageResult<Consent>>(`${this.baseUrl}/consents`, { params: this.pageParams(query) });
  }

  getMyPermissions(): Observable<{ userId?: string; userName?: string; roles: string[]; permissions: string[] }> {
    return this.http.get<{ userId?: string; userName?: string; roles: string[]; permissions: string[] }>(`${this.baseUrl}/me/permissions`);
  }

  getIdentitySettings(): Observable<IdentitySetting[]> {
    return this.http.get<IdentitySetting[]>(`${this.baseUrl}/settings`);
  }

  saveIdentitySettings(settings: Array<{ key: string; value: unknown }>): Observable<IdentitySetting[]> {
    // SystemSetting.Value is persisted as text. Normalize booleans and numbers
    // before serialization so ASP.NET can bind BulkUpdateSettingItem.Value.
    const payload = settings.map(setting => ({
      key: setting.key,
      value: String(setting.value ?? ''),
    }));
    return this.http.put<IdentitySetting[]>(`${this.baseUrl}/settings/bulk`, { settings: payload });
  }

  getDashboardStats(): Observable<DashboardStats> {
    return this.http.get<DashboardStats>(`${this.baseUrl}/dashboard`);
  }

  private pageParams(query: AdminPageQuery): Record<string, string> {
    const params: Record<string, string> = { page: String(query.page), pageSize: String(query.pageSize) };
    if (query.search?.trim()) params['search'] = query.search.trim();
    if (query.cursor) params['cursor'] = query.cursor;
    if (query.sort) {
      const sort = Array.isArray(query.sort) ? query.sort : [query.sort];
      params['sort'] = sort.map(term => `${term.key}:${term.direction}`).join(',');
    }
    for (const [key, value] of Object.entries(query.filters ?? {})) {
      if (value !== null && value !== undefined && value !== '') params[key] = String(value);
    }
    return params;
  }
}
