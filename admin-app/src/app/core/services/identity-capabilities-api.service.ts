import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  AuditLogRow,
  DeliveryHealth,
  DevicePostureAssessment,
  DevicePostureEvaluation,
  DevicePostureEvidence,
  DevicePosturePolicy,
  IdentitySetting,
  MtlsBinding,
  ProvisioningJob,
  ProvisioningReadiness,
  RadiusEapTlsStatus,
  SecuritySignalStatus,
  SecuritySignalOutboxEntry,
} from "../contracts/admin.contracts";

export type {
  AuditLogRow,
  DeliveryHealth,
  DevicePostureAssessment,
  DevicePostureEvaluation,
  DevicePostureEvidence,
  DevicePosturePolicy,
  IdentitySetting,
  MtlsBinding,
  ProvisioningJob,
  ProvisioningReadiness,
  RadiusEapTlsStatus,
  SecuritySignalStatus,
  SecuritySignalOutboxEntry,
} from "../contracts/admin.contracts";

export interface IdentityCapabilitiesAuditPage {
  items: AuditLogRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: "root" })
export class IdentityCapabilitiesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.adminApiUrl;

  getDevicePosturePolicy(facilityId?: string): Observable<DevicePosturePolicy> {
    return this.http.get<DevicePosturePolicy>(
      `${this.baseUrl}/device-posture/policy`,
    );
  }

  updateDevicePosturePolicy(
    policy: Pick<
      DevicePosturePolicy,
      "mode" | "providers" | "evidenceTtlSeconds" | "requiredSignals"
    >,
    facilityId?: string,
  ): Observable<DevicePosturePolicy> {
    return this.http.put<DevicePosturePolicy>(
      `${this.baseUrl}/device-posture/policy`,
      { ...policy, facilityId },
    );
  }

  rollbackDevicePosturePolicy(
    facilityId?: string,
  ): Observable<DevicePosturePolicy> {
    return this.http.post<DevicePosturePolicy>(
      `${this.baseUrl}/device-posture/policy/rollback`,
      {},
      facilityId ? { params: { facilityId } } : {},
    );
  }

  previewDevicePosture(
    evidence: DevicePostureEvidence,
    facilityId?: string,
  ): Observable<DevicePostureEvaluation> {
    return this.http.post<DevicePostureEvaluation>(
      `${this.baseUrl}/device-posture/preview`,
      { ...evidence, facilityId },
    );
  }

  getDevicePostureAssessments(
    facilityId?: string,
  ): Observable<DevicePostureAssessment[]> {
    return this.http.get<DevicePostureAssessment[]>(
      `${this.baseUrl}/device-posture/assessments`,
      facilityId ? { params: { facilityId } } : {},
    );
  }

  getIdentitySettings(facilityId?: string): Observable<IdentitySetting[]> {
    return this.http.get<IdentitySetting[]>(
      `${this.baseUrl}/settings`,
      facilityId ? { params: { facilityId } } : {},
    );
  }

  getAuditLogs(query: {
    page?: number;
    pageSize?: number;
  }): Observable<IdentityCapabilitiesAuditPage> {
    const params: Record<string, string> = {
      page: String(query.page ?? 1),
      pageSize: String(query.pageSize ?? 50),
    };
    return this.http.get<IdentityCapabilitiesAuditPage>(
      `${this.baseUrl.replace(/\/admin$/, "")}/audit-logs`,
      { params },
    );
  }

  getProvisioningJobs(): Observable<ProvisioningJob[]> {
    return this.http.get<ProvisioningJob[]>(
      `${this.baseUrl}/provisioning/jobs`,
    );
  }

  getProvisioningReadiness(): Observable<ProvisioningReadiness> {
    return this.http.get<ProvisioningReadiness>(
      `${this.baseUrl}/provisioning/readiness`,
    );
  }

  queueProvisioning(request: {
    target: string;
    operation: string;
    resourceType: string;
    resourceId: string;
    payload: unknown;
    externalId?: string;
  }): Observable<ProvisioningJob> {
    return this.http.post<ProvisioningJob>(
      `${this.baseUrl}/provisioning/queue`,
      request,
    );
  }

  retryProvisioningJob(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(
      `${this.baseUrl}/provisioning/jobs/${encodeURIComponent(id)}/retry`,
      {},
    );
  }

  getMtlsBindings(): Observable<MtlsBinding[]> {
    return this.http.get<MtlsBinding[]>(`${this.baseUrl}/mtls/bindings`);
  }

  getRadiusEapTlsStatus(): Observable<RadiusEapTlsStatus> {
    return this.http.get<RadiusEapTlsStatus>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/radius/eap-tls/status`,
    );
  }

  getSecuritySignalStatus(): Observable<SecuritySignalStatus> {
    return this.http.get<SecuritySignalStatus>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/security-signals/status`,
    );
  }

  getSecuritySignalOutbox(): Observable<SecuritySignalOutboxEntry[]> {
    return this.http.get<SecuritySignalOutboxEntry[]>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/security-signals/outbox`,
    );
  }

  retrySecuritySignal(id: string): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(
      `${this.baseUrl.replace(/\/admin$/, "")}/admin/security-signals/outbox/${encodeURIComponent(id)}/retry`,
      {},
    );
  }

  queueProvisioningRetry(
    id: string,
  ): Observable<{ id: string; status: string }> {
    return this.retryProvisioningJob(id);
  }

  getDeliveryHealth(): Observable<DeliveryHealth> {
    return this.http.get<DeliveryHealth>(
      `${this.baseUrl}/provisioning/delivery-health`,
    );
  }

  revokeMtlsBinding(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/mtls/bindings/${encodeURIComponent(id)}`,
    );
  }

  exportAuditCsv(): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl.replace(/\/admin$/, "")}/audit-logs/export`,
      { responseType: "blob" },
    );
  }
}
