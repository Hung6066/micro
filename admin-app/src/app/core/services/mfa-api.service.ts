import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export interface MfaStatus {
  enabled: boolean;
  enrolledAt?: string;
  recoveryCodesRemaining: number;
}

export interface MfaEnrollment {
  secretKey: string;
  qrCodeUri: string;
  recoveryCodes: string[];
}

@Injectable({ providedIn: "root" })
export class MfaApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.authApiUrl;

  getStatus(): Observable<MfaStatus> {
    return this.http.get<MfaStatus>(`${this.baseUrl}/mfa/status`);
  }

  enroll(): Observable<MfaEnrollment> {
    return this.http.post<MfaEnrollment>(`${this.baseUrl}/mfa/enroll`, {});
  }

  verify(code: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/mfa/verify`, { code });
  }
}
