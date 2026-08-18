import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

export interface AdminPermissionSnapshot {
  userId?: string;
  userName?: string;
  roles: string[];
  permissions: string[];
  scopes?: string[];
  facilityIds?: string[];
  authzVersion?: string;
}

/** Permission API for shell navigation and capability guards. */
@Injectable({ providedIn: "root" })
export class AdminPermissionsApiService {
  private readonly http = inject(HttpClient);

  getCurrent(): Observable<AdminPermissionSnapshot> {
    return this.http.get<AdminPermissionSnapshot>(
      `${environment.adminApiUrl}/me/permissions`,
    );
  }
}
