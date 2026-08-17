import { Injectable, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, forkJoin, of, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import {
  AdminApiService,
  AuditLogRow,
  DevicePostureAssessment,
  DevicePosturePolicy,
  DeliveryHealth,
  IdentitySetting,
  MtlsBinding,
  ProvisioningJob,
  RadiusEapTlsStatus,
  SecuritySignalStatus,
} from './admin-api.service';

export interface IdentityCapabilityError {
  status: number;
  code: string;
  correlationId?: string;
}

export interface IdentityCapabilityState {
  policy?: DevicePosturePolicy;
  assessments: DevicePostureAssessment[];
  settings: IdentitySetting[];
  auditRows: AuditLogRow[];
  provisioningJobs: ProvisioningJob[];
  mtlsBindings: MtlsBinding[];
  radiusStatus?: RadiusEapTlsStatus;
  ssfStatus?: SecuritySignalStatus;
  deliveryHealth?: DeliveryHealth;
}

/** Typed admin facade. Vendor credentials, certificate material and raw posture
 * evidence never cross this boundary into the browser. */
@Injectable({ providedIn: 'root' })
export class IdentityCapabilitiesService {
  private readonly api = inject(AdminApiService);

  loadState(): Observable<IdentityCapabilityState> {
    return forkJoin({
      policy: this.api.getDevicePosturePolicy().pipe(catchError(() => of(undefined))),
      assessments: this.api.getDevicePostureAssessments().pipe(catchError(() => of([]))),
      settings: this.api.getIdentitySettings().pipe(catchError(() => of([]))),
      audit: this.api.getAuditLogs({ page: 1, pageSize: 25 }).pipe(catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 25 }))),
      provisioningJobs: this.api.getProvisioningJobs().pipe(catchError(() => of([]))),
      mtlsBindings: this.api.getMtlsBindings().pipe(catchError(() => of([]))),
      radiusStatus: this.api.getRadiusEapTlsStatus().pipe(catchError(() => of(undefined))),
      ssfStatus: this.api.getSecuritySignalStatus().pipe(catchError(() => of(undefined))),
      deliveryHealth: this.api.getDeliveryHealth().pipe(catchError(() => of(undefined))),
    }).pipe(map(({ policy, assessments, settings, audit, provisioningJobs, mtlsBindings, radiusStatus, ssfStatus, deliveryHealth }) => ({
      policy,
      assessments,
      settings,
      auditRows: audit.items ?? [],
      provisioningJobs,
      mtlsBindings,
      radiusStatus,
      ssfStatus,
      deliveryHealth,
    })));
  }

  normalizeError(error: unknown): IdentityCapabilityError {
    const response = error instanceof HttpErrorResponse ? error : undefined;
    const headers = response?.headers;
    return {
      status: response?.status ?? 0,
      code: response?.error?.code ?? response?.error?.errorCode ?? `http_${response?.status ?? 'unknown'}`,
      correlationId: headers?.get('x-correlation-id') ?? headers?.get('trace-id') ?? undefined,
    };
  }

  fail<T>(error: unknown): Observable<T> {
    return throwError(() => this.normalizeError(error));
  }
}
