// @ts-nocheck
import { Provider } from '@angular/core';

import { PatientService } from '@core/services/patient.service';
import { MockPatientService } from '@core/services/mock/mock-patient.service';

import { ClinicalService } from '@core/services/clinical.service';
import { MockClinicalService } from '@core/services/mock/mock-clinical.service';

import { AppointmentService } from '@core/services/appointment.service';
import { MockAppointmentService } from '@core/services/mock/mock-appointment.service';

import { PharmacyService } from '@core/services/pharmacy.service';
import { MockPharmacyService } from '@core/services/mock/mock-pharmacy.service';

import { LabService } from '@core/services/lab.service';
import { MockLabService } from '@core/services/mock/mock-lab.service';

import { BillingService } from '@core/services/billing.service';
import { MockBillingService } from '@core/services/mock/mock-billing.service';

import { AuthService } from '@core/services/auth.service';
import { MockAuthService } from '@core/services/mock/mock-auth.service';

import { DashboardService } from '@core/services/dashboard.service';
import { MockDashboardService } from '@core/services/mock/mock-dashboard.service';

import { AdminService } from '@core/services/admin.service';
import { MockAdminService } from '@core/services/mock/mock-admin.service';

/**
 * Provider array that replaces every real service with its mock counterpart.
 * Import and spread into the `providers` array of AppModule when
 * `environment.useMockServices` is `true`.
 */
export const mockServiceProviders: Provider[] = [
  { provide: PatientService, useClass: MockPatientService },
  { provide: ClinicalService, useClass: MockClinicalService },
  { provide: AppointmentService, useClass: MockAppointmentService },
  { provide: PharmacyService, useClass: MockPharmacyService },
  { provide: LabService, useClass: MockLabService },
  { provide: BillingService, useClass: MockBillingService },
  { provide: AuthService, useClass: MockAuthService },
  { provide: DashboardService, useClass: MockDashboardService },
  { provide: AdminService, useClass: MockAdminService },
];
