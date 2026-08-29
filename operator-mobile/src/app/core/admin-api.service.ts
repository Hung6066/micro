import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable, firstValueFrom } from "rxjs";
import type {
  HisHopeNotification,
  HisHopeNotificationInboxApi,
  HisHopeNotificationPage,
} from "@his-hope/mobile-foundation";
import { environment } from "../../environments/environment";

export type {
  Consent as MobileConsent,
  DashboardStats as MobileDashboardStats,
  MobileResource,
  OidcClient as MobileClient,
  Role as MobileRole,
  User as MobileUser,
} from "./contracts/mobile.contracts";

export interface MobileMfaEnrollment {
  secretKey: string;
  qrCodeUri: string;
  recoveryCodes: string[];
}
export interface MobileMfaStatus {
  enabled: boolean;
  requiresMfa: boolean;
  enrolledAt?: string;
  recoveryCodesRemaining: number;
}
export type MobileNotification = HisHopeNotification;
export type MobileNotificationPage = HisHopeNotificationPage;

/** Mobile platform API: MFA, notifications, passkeys — not resource CRUD. */
@Injectable({ providedIn: "root" })
export class MobileAdminApiService implements HisHopeNotificationInboxApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;
  private readonly authApiUrl = this.baseUrl.replace(/\/admin$/, "/auth");
  private readonly mobileApiUrl = this.baseUrl.replace(/\/admin$/, "");

  getMyPermissions(): Observable<{ permissions: string[]; roles: string[] }> {
    return this.http.get<{ permissions: string[]; roles: string[] }>(
      `${this.authApiUrl}/me/permissions`,
    );
  }
  enrollMfa(): Observable<MobileMfaEnrollment> {
    return this.http.post<MobileMfaEnrollment>(
      `${this.authApiUrl}/mfa/enroll`,
      {},
    );
  }
  getMfaStatus(): Observable<MobileMfaStatus> {
    return this.http.get<MobileMfaStatus>(`${this.authApiUrl}/mfa/status`);
  }
  verifyMfa(
    code: string,
  ): Observable<{ status: string; requiresMfa: boolean }> {
    return this.http.post<{ status: string; requiresMfa: boolean }>(
      `${this.authApiUrl}/mfa/verify`,
      { code },
    );
  }
  getNotifications(
    page = 1,
    pageSize = 30,
  ): Observable<MobileNotificationPage> {
    return this.http.get<MobileNotificationPage>(
      `${this.mobileApiUrl}/mobile/notifications`,
      { params: { page, pageSize } },
    );
  }
  list(page = 1, pageSize = 30): Promise<MobileNotificationPage> {
    return firstValueFrom(this.getNotifications(page, pageSize));
  }
  markNotificationRead(id: string): Observable<void> {
    return this.http.post<void>(
      `${this.mobileApiUrl}/mobile/notifications/${encodeURIComponent(id)}/read`,
      {},
    );
  }
  markRead(id: string): Promise<void> {
    return firstValueFrom(this.markNotificationRead(id));
  }
  markAllNotificationsRead(): Observable<{ updated: number }> {
    return this.http.post<{ updated: number }>(
      `${this.mobileApiUrl}/mobile/notifications/read-all`,
      {},
    );
  }
  markAllRead(): Promise<{ updated: number }> {
    return firstValueFrom(this.markAllNotificationsRead());
  }
  registerPasskeyOptions(): Observable<Record<string, unknown>> {
    return this.http.post<Record<string, unknown>>(
      `${this.authApiUrl}/passkeys/register/options`,
      {},
    );
  }
  completePasskeyRegistration(
    response: Readonly<Record<string, unknown>>,
  ): Observable<{ registered: boolean }> {
    return this.http.post<{ registered: boolean }>(
      `${this.authApiUrl}/passkeys/register/complete`,
      response,
    );
  }
  nativeMfaOptions(
    ticket: string,
  ): Observable<{ options: Record<string, unknown> }> {
    return this.http.post<{ options: Record<string, unknown> }>(
      `${this.authApiUrl}/passkeys/mfa/native/options`,
      { ticket },
    );
  }
  completeNativeMfa(
    ticket: string,
    response: Readonly<Record<string, unknown>>,
  ): Observable<{ approved: boolean }> {
    return this.http.post<{ approved: boolean }>(
      `${this.authApiUrl}/passkeys/mfa/native/complete`,
      { ticket, response },
    );
  }
}
