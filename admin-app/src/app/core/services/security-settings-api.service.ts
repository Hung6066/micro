import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { IdentitySetting } from "../contracts/admin.contracts";

@Injectable({ providedIn: "root" })
export class SecuritySettingsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getIdentitySettings(facilityId?: string): Observable<IdentitySetting[]> {
    return this.http.get<IdentitySetting[]>(
      `${this.baseUrl}/settings`,
      this.facilityParams(facilityId),
    );
  }

  saveIdentitySettings(
    settings: Array<{ key: string; value: unknown }>,
    facilityId?: string,
  ): Observable<IdentitySetting[]> {
    const payload = settings.map((setting) => ({
      key: setting.key,
      value: String(setting.value ?? ""),
    }));
    return this.http.put<IdentitySetting[]>(
      `${this.baseUrl}/settings/bulk`,
      { settings: payload },
      this.facilityParams(facilityId),
    );
  }

  private facilityParams(facilityId?: string): {
    params?: { facilityId: string };
  } {
    return facilityId ? { params: { facilityId } } : {};
  }
}
