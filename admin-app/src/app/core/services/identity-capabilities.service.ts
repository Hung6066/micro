import { Injectable, inject } from "@angular/core";
import { Observable, forkJoin, of, throwError } from "rxjs";
import { catchError, map } from "rxjs/operators";
import { ApiErrorMessageService } from "./api-error-message.service";
import {
  AuditLogRow,
  DevicePostureAssessment,
  DevicePosturePolicy,
  DeliveryHealth,
  IdentitySetting,
  MtlsBinding,
  ProvisioningJob,
  RadiusEapTlsStatus,
  SecuritySignalStatus,
} from "./identity-capabilities-api.service";
import { IdentityCapabilitiesApiService } from "./identity-capabilities-api.service";

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
@Injectable({ providedIn: "root" })
export class IdentityCapabilitiesService {
  private readonly api = inject(IdentityCapabilitiesApiService);
  private readonly errorMessages = inject(ApiErrorMessageService);

  loadState(facilityId?: string): Observable<IdentityCapabilityState> {
    return forkJoin({
      policy: this.api
        .getDevicePosturePolicy(facilityId)
        .pipe(catchError(() => of(undefined))),
      assessments: this.api
        .getDevicePostureAssessments(facilityId)
        .pipe(catchError(() => of([]))),
      settings: this.api
        .getIdentitySettings(facilityId)
        .pipe(catchError(() => of([]))),
      audit: this.api
        .getAuditLogs({ page: 1, pageSize: 25 })
        .pipe(
          catchError(() =>
            of({ items: [], totalCount: 0, page: 1, pageSize: 25 }),
          ),
        ),
      provisioningJobs: this.api
        .getProvisioningJobs()
        .pipe(catchError(() => of([]))),
      mtlsBindings: this.api.getMtlsBindings().pipe(catchError(() => of([]))),
      radiusStatus: this.api
        .getRadiusEapTlsStatus()
        .pipe(catchError(() => of(undefined))),
      ssfStatus: this.api
        .getSecuritySignalStatus()
        .pipe(catchError(() => of(undefined))),
      deliveryHealth: this.api
        .getDeliveryHealth()
        .pipe(catchError(() => of(undefined))),
    }).pipe(
      map(
        ({
          policy,
          assessments,
          settings,
          audit,
          provisioningJobs,
          mtlsBindings,
          radiusStatus,
          ssfStatus,
          deliveryHealth,
        }) => ({
          policy,
          assessments,
          settings,
          auditRows: audit.items ?? [],
          provisioningJobs,
          mtlsBindings,
          radiusStatus,
          ssfStatus,
          deliveryHealth,
        }),
      ),
    );
  }

  normalizeError(error: unknown): IdentityCapabilityError {
    return this.errorMessages.normalize(error);
  }

  fail<T>(error: unknown): Observable<T> {
    return throwError(() => this.normalizeError(error));
  }
}
