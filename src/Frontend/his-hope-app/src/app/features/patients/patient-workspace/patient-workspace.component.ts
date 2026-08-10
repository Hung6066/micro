import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject, forkJoin, takeUntil } from 'rxjs';

import { PatientService } from '@core/services/patient.service';
import { AuthService } from '@core/services/auth.service';
import { AppointmentService } from '@core/services/appointment.service';

import { Patient } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';
import { Invoice } from '@core/models/invoice.model';

import { StartEncounterDialogComponent, StartEncounterData } from './dialogs/start-encounter.dialog';
import { OrderLabDialogComponent, OrderLabData } from './dialogs/order-lab.dialog';
import { PrescribeDialogComponent, PrescribeData } from './dialogs/prescribe.dialog';
import { ScheduleDialogComponent, ScheduleData } from './dialogs/schedule.dialog';
import { RecordPaymentDialogComponent, RecordPaymentData } from './dialogs/record-payment.dialog';
import { HisHopeTranslatePipe, HisHopeI18nService } from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-patient-workspace',
    imports: [
        CommonModule,
        RouterModule,
        MatTabsModule,
        MatTableModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatTooltipModule,
        HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './patient-workspace.component.html',
    styleUrls: ['./patient-workspace.component.scss']
})
export class PatientWorkspaceComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  patient?: Patient;
  error: string | null = null;

  // Tab data
  encounters: Encounter[] = [];
  appointments: Appointment[] = [];
  labOrders: LabOrder[] = [];
  prescriptions: Prescription[] = [];
  invoices: Invoice[] = [];

  loadingEncounters = false;
  loadingAppointments = false;
  loadingLabs = false;
  loadingPrescriptions = false;
  loadingInvoices = false;

  expandedEncounterId: string | null = null;

  // Column definitions
  encounterColumns = ['encounterDate', 'encounterType', 'chiefComplaint', 'status', 'actions'];
  appointmentColumns = ['scheduledDate', 'startTime', 'type', 'status', 'actions'];
  labColumns = ['orderDate', 'testName', 'status', 'result'];
  prescriptionColumns = ['prescribedAt', 'medicationName', 'dosageInstructions', 'quantity', 'status'];
  invoiceColumns = ['invoiceNumber', 'invoiceDate', 'totalAmount', 'balanceDue', 'status'];

  get activeConditions() {
    return this.patient?.conditions.filter(c => c.isActive) || [];
  }

  allergyTooltip(a: any): string {
    const reaction = a.reaction || this.i18n.t('patient.unknown', 'Không rõ');
    const severity = a.severity || 'N/A';
    return this.i18n.t('patient.allergyReaction', 'Phản ứng: {{reaction}}', { reaction })
      + ' | ' + this.i18n.t('patient.allergySeverity', 'Mức độ: {{severity}}', { severity });
  }

  conditionTooltip(c: any): string {
    const chronicity = c.isChronic
      ? this.i18n.t('patient.chronic', 'Mạn tính')
      : this.i18n.t('patient.acute', 'Cấp tính');
    const code = c.icd10Code || 'N/A';
    return chronicity + ' | ' + this.i18n.t('patient.icd10', 'ICD-10: {{code}}', { code });
  }

  private patientId = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private patientService: PatientService,
    private appointmentService: AppointmentService,
    private authService: AuthService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.patientId = this.route.snapshot.params['id'];
    this.loadPatient();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPatient(): void {
    this.error = null;
    this.patientService.getById(this.patientId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (p) => {
          this.patient = p;
          this.loadAllTabData();
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t('patients.loadFailed', 'Không thể tải thông tin bệnh nhân');
          this.cdr.markForCheck();
        },
      });
  }

  private loadAllTabData(): void {
    this.loadEncounters();
    this.loadAppointments();
    this.loadLabOrders();
    this.loadPrescriptions();
    this.loadInvoices();
  }

  private loadEncounters(): void {
    this.loadingEncounters = true;
    this.patientService.getEncounters(this.patientId, 1, 20)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.encounters = res.items; this.loadingEncounters = false; this.cdr.markForCheck(); },
        error: () => { this.loadingEncounters = false; this.cdr.markForCheck(); },
      });
  }

  private loadAppointments(): void {
    this.loadingAppointments = true;
    this.patientService.getAppointments(this.patientId, 1, 20)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.appointments = res.items; this.loadingAppointments = false; this.cdr.markForCheck(); },
        error: () => { this.loadingAppointments = false; this.cdr.markForCheck(); },
      });
  }

  private loadLabOrders(): void {
    this.loadingLabs = true;
    this.patientService.getLabOrders(this.patientId, 1, 20)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.labOrders = res.items; this.loadingLabs = false; this.cdr.markForCheck(); },
        error: () => { this.loadingLabs = false; this.cdr.markForCheck(); },
      });
  }

  private loadPrescriptions(): void {
    this.loadingPrescriptions = true;
    this.patientService.getPrescriptions(this.patientId, 1, 20)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.prescriptions = res.items; this.loadingPrescriptions = false; this.cdr.markForCheck(); },
        error: () => { this.loadingPrescriptions = false; this.cdr.markForCheck(); },
      });
  }

  private loadInvoices(): void {
    this.loadingInvoices = true;
    this.patientService.getInvoices(this.patientId, 1, 20)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.invoices = res.items; this.loadingInvoices = false; this.cdr.markForCheck(); },
        error: () => { this.loadingInvoices = false; this.cdr.markForCheck(); },
      });
  }

  // ── Encounter ──

  toggleEncounterDetail(encounter: Encounter): void {
    if (this.expandedEncounterId === encounter.id) {
      this.expandedEncounterId = null;
    } else {
      this.expandedEncounterId = encounter.id;
    }
  }

  startNewEncounter(): void {
    const dialogRef = this.dialog.open(StartEncounterDialogComponent, {
      width: '520px',
      data: {
        patientId: this.patientId,
        patientName: this.patient?.fullName,
      } as StartEncounterData,
    });
    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe((result) => {
      if (result) {
        this.loadEncounters();
      }
    });
  }

  // ── Appointments ──

  checkInAppointment(id: string): void {
    this.appointmentService.checkIn(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('patients.checkInSuccess', 'Đã check-in lịch hẹn'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadAppointments();
        },
        error: () => this.snackBar.open(this.i18n.t('patients.checkInFailed', 'Không thể check-in'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }

  checkOutAppointment(id: string): void {
    this.appointmentService.checkOut(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('patients.checkOutSuccess', 'Đã check-out lịch hẹn'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadAppointments();
        },
        error: () => this.snackBar.open(this.i18n.t('patients.checkOutFailed', 'Không thể check-out'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }

  cancelAppointment(id: string): void {
    this.appointmentService.cancel(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('patients.cancelAppointmentSuccess', 'Đã hủy lịch hẹn'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadAppointments();
        },
        error: () => this.snackBar.open(this.i18n.t('patients.cancelAppointmentFailed', 'Không thể hủy lịch hẹn'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }

  // ── Lab Orders ──

  hasAbnormalFlag(order: LabOrder): boolean {
    return order.tests?.some(t => t.result?.abnormalFlagCode && t.result.abnormalFlagCode !== 'none' && t.result.abnormalFlagCode !== 'normal') ?? false;
  }

  isCompleted(order: LabOrder): boolean {
    return order.tests?.every(t => t.statusCode === 'completed') ?? false;
  }

  // ── Dialogs ──

  orderLab(): void {
    const dialogRef = this.dialog.open(OrderLabDialogComponent, {
      width: '460px',
      data: {
        patientId: this.patientId,
        patientName: this.patient?.fullName,
      } as OrderLabData,
    });
    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe((result) => {
      if (result) this.loadLabOrders();
    });
  }

  prescribe(): void {
    const dialogRef = this.dialog.open(PrescribeDialogComponent, {
      width: '500px',
      data: {
        patientId: this.patientId,
        patientName: this.patient?.fullName,
      } as PrescribeData,
    });
    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe((result) => {
      if (result) this.loadPrescriptions();
    });
  }

  scheduleAppointment(): void {
    const dialogRef = this.dialog.open(ScheduleDialogComponent, {
      width: '460px',
      data: {
        patientId: this.patientId,
        patientName: this.patient?.fullName,
      } as ScheduleData,
    });
    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe((result) => {
      if (result) this.loadAppointments();
    });
  }

  recordPayment(): void {
    const dialogRef = this.dialog.open(RecordPaymentDialogComponent, {
      width: '500px',
      data: {
        patientId: this.patientId,
        patientName: this.patient?.fullName,
        invoices: this.invoices,
      } as RecordPaymentData,
    });
    dialogRef.afterClosed().pipe(takeUntil(this.destroy$)).subscribe((result) => {
      if (result) this.loadInvoices();
    });
  }
}
