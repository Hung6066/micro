import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  AdminSession,
  BulkImportPreview,
  BulkImportResult,
  ProvisioningJob,
} from "../contracts/admin.contracts";
import {
  IDENTITY_WORKBENCH_ACTIONS,
  IDENTITY_WORKBENCH_RESOURCES,
  identityWorkbenchActionPath,
  identityWorkbenchPath,
} from "../contracts/identity-workbench.naming";

@Injectable({ providedIn: "root" })
export class IdentityOperationsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getAdminSessions(
    userId: string,
  ): Observable<{ userId: string; sessions: AdminSession[] }> {
    return this.http.get<{ userId: string; sessions: AdminSession[] }>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.users, userId)}/sessions`,
    );
  }

  revokeAdminSession(
    userId: string,
    sessionId: string,
    reason: string,
  ): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}${identityWorkbenchPath(IDENTITY_WORKBENCH_RESOURCES.users, userId)}/sessions/${encodeURIComponent(sessionId)}`,
      { params: { reason } },
    );
  }

  revokeAllAdminSessions(
    userId: string,
    reason: string,
  ): Observable<{ userId: string; revokedSessions: number }> {
    return this.http.post<{ userId: string; revokedSessions: number }>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.users, userId, "sessions/revoke-all")}`,
      { reason },
    );
  }

  resetAdminCredentials(
    userId: string,
    request: { resetMfa: boolean; revokePasskeys: boolean; reason: string },
  ): Observable<{
    userId: string;
    removedMfa: number;
    removedPasskeys: number;
    tokensRevoked: boolean;
  }> {
    return this.http.post<{
      userId: string;
      removedMfa: number;
      removedPasskeys: number;
      tokensRevoked: boolean;
    }>(
      `${this.baseUrl}${identityWorkbenchActionPath(IDENTITY_WORKBENCH_RESOURCES.users, userId, "credentials/reset")}`,
      request,
    );
  }

  previewUserImport(file: File): Observable<BulkImportPreview> {
    return this.http.post<BulkImportPreview>(
      `${this.baseUrl}/users/bulk/preview`,
      file,
      {
        headers: { "Content-Type": file.type || "text/csv" },
      },
    );
  }

  importUsers(file: File): Observable<BulkImportResult> {
    return this.http.post<BulkImportResult>(
      `${this.baseUrl}/users/bulk/file`,
      file,
      {
        headers: { "Content-Type": file.type || "text/csv" },
      },
    );
  }

  reconcileProvisioning(
    target: "scim" | "entra" | "google-workspace",
  ): Observable<{ target: string; queued: number; sourceCount: number }> {
    return this.http.post<{
      target: string;
      queued: number;
      sourceCount: number;
    }>(
      `${this.baseUrl}/provisioning/reconcile/${encodeURIComponent(target)}`,
      {},
    );
  }

  retrySecuritySignal(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/security-signals/outbox/${encodeURIComponent(id)}/retry`,
      {},
    );
  }

  getProvisioningJobs(): Observable<ProvisioningJob[]> {
    return this.http.get<ProvisioningJob[]>(
      `${this.baseUrl}/provisioning/jobs`,
    );
  }
}
