import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { HisHopePageQuery, HisHopePageResult } from '@his-hope/frontend-foundation';
import type { HisHopeNotification, HisHopeNotificationPage, HisHopeNotificationInboxApi } from '@his-hope/mobile-foundation';
import { environment } from '../../environments/environment';

export interface MobileDashboardStats { totalClients: number; totalUsers: number; totalRoles: number; totalConsents: number; }
export interface MobileClient { id?: string; clientId: string; displayName: string; clientType: string; redirectUris: string[]; }
export interface MobileUser { id: string; userName: string; email: string; roles: string[]; isActive: boolean; }
export interface MobileRole { id?: string; name: string; description?: string; }
export interface MobileConsent { id: string; subject: string; clientId: string; scopes: string[]; created: string; }
export interface MobileMfaEnrollment { secretKey: string; qrCodeUri: string; recoveryCodes: string[]; }
export interface MobileMfaStatus { enabled: boolean; requiresMfa: boolean; enrolledAt?: string; recoveryCodesRemaining: number; }
export type MobileNotification = HisHopeNotification;
export type MobileNotificationPage = HisHopeNotificationPage;
export type MobileResource = 'clients' | 'users' | 'roles' | 'consents';

@Injectable({ providedIn: 'root' })
export class MobileAdminApiService implements HisHopeNotificationInboxApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;
  private readonly authApiUrl = this.baseUrl.replace(/\/admin$/, '/auth');
  private readonly mobileApiUrl = this.baseUrl.replace(/\/admin$/, '');

  getDashboard(): Observable<MobileDashboardStats> { return this.http.get<MobileDashboardStats>(`${this.baseUrl}/dashboard`); }
  getMyPermissions(): Observable<{ permissions: string[]; roles: string[] }> { return this.http.get<{ permissions: string[]; roles: string[] }>(`${this.baseUrl}/me/permissions`); }
  enrollMfa(): Observable<MobileMfaEnrollment> { return this.http.post<MobileMfaEnrollment>(`${this.authApiUrl}/mfa/enroll`, {}); }
  getMfaStatus(): Observable<MobileMfaStatus> { return this.http.get<MobileMfaStatus>(`${this.authApiUrl}/mfa/status`); }
  verifyMfa(code: string): Observable<{ status: string; requiresMfa: boolean }> { return this.http.post<{ status: string; requiresMfa: boolean }>(`${this.authApiUrl}/mfa/verify`, { code }); }
  getNotifications(page = 1, pageSize = 30): Observable<MobileNotificationPage> {
    return this.http.get<MobileNotificationPage>(`${this.mobileApiUrl}/mobile/notifications`, { params: { page, pageSize } });
  }
  list(page = 1, pageSize = 30): Promise<MobileNotificationPage> { return firstValueFrom(this.getNotifications(page, pageSize)); }
  markNotificationRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.mobileApiUrl}/mobile/notifications/${encodeURIComponent(id)}/read`, {});
  }
  markRead(id: string): Promise<void> { return firstValueFrom(this.markNotificationRead(id)); }
  markAllNotificationsRead(): Observable<{ updated: number }> {
    return this.http.post<{ updated: number }>(`${this.mobileApiUrl}/mobile/notifications/read-all`, {});
  }
  markAllRead(): Promise<{ updated: number }> { return firstValueFrom(this.markAllNotificationsRead()); }
  registerPasskeyOptions(): Observable<Record<string, unknown>> { return this.http.post<Record<string, unknown>>(`${this.authApiUrl}/passkeys/register/options`, {}); }
  completePasskeyRegistration(response: Readonly<Record<string, unknown>>): Observable<{ registered: boolean }> {
    return this.http.post<{ registered: boolean }>(`${this.authApiUrl}/passkeys/register/complete`, response);
  }
  nativeMfaOptions(ticket: string): Observable<{ options: Record<string, unknown> }> {
    return this.http.post<{ options: Record<string, unknown> }>(`${this.authApiUrl}/passkeys/mfa/native/options`, { ticket });
  }
  completeNativeMfa(ticket: string, response: Readonly<Record<string, unknown>>): Observable<{ approved: boolean }> {
    return this.http.post<{ approved: boolean }>(`${this.authApiUrl}/passkeys/mfa/native/complete`, { ticket, response });
  }

  getPage<T>(resource: MobileResource, query: HisHopePageQuery): Observable<HisHopePageResult<T>> {
    let params = new HttpParams().set('page', String(query.page)).set('pageSize', String(query.pageSize));
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    if (query.cursor) params = params.set('cursor', query.cursor);
    const sorts = query.sort ? (Array.isArray(query.sort) ? query.sort : [query.sort]) : [];
    if (sorts.length) params = params.set('sort', sorts.map(sort => `${sort.key}:${sort.direction}`).join(','));
    Object.entries(query.filters ?? {}).forEach(([key, value]) => { if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value)); });
    return this.http.get<HisHopePageResult<T>>(`${this.baseUrl}/${resource}`, { params });
  }

  bulk(resource: Exclude<MobileResource, 'consents'>, actionId: string, rowKeys: string[]): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/tables/${resource}/bulk`, { actionId, rowKeys, selection: 'page' });
  }
}
