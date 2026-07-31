import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, forkJoin, takeUntil, debounceTime, distinctUntilChanged } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { AuthService } from '@core/services/auth.service';
import { DashboardService, DashboardStats } from '@core/services/dashboard.service';
import { PatientService } from '@core/services/patient.service';
import { AppointmentService } from '@core/services/appointment.service';
import { ClinicalService } from '@core/services/clinical.service';
import { Encounter } from '@core/models/encounter.model';
import { Appointment } from '@core/models/appointment.model';
import { Patient } from '@core/models/patient.model';
import {
    HisHopeDataTableComponent,
    HisHopeMetricCardComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-dashboard',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatCardModule, MatInputModule, MatFormFieldModule, MatIconModule, MatButtonModule,
        MatTableModule, MatChipsModule, MatAutocompleteModule,
        HisHopeDataTableComponent, HisHopeMetricCardComponent, HisHopePageLayoutComponent,
        HisHopeStateComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './dashboard.component.html',
    styleUrls: ['./dashboard.component.scss'],
})
export class DashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  // Patient search
  patientSearchControl = new FormControl('');
  patientSearchResults: Patient[] = [];
  recentPatients: Patient[] = [];

  stats: DashboardStats = {
    totalPatients: 0,
    todayAppointments: 0,
    activeEncounters: 0,
    pendingDiagnoses: 0,
    pendingLabs: 0,
    outstandingInvoices: 0,
    lowStockMedications: 0,
    newPatientsToday: 0,
    appointmentsTomorrow: 0,
    recentEncounters: [],
    upcomingAppointments: [],
  };

  recentEncounters: Encounter[] = [];
  upcomingAppointments: Appointment[] = [];
  currentUserName = 'User';
  today = new Date();
  loading = true;
  error: string | null = null;

  readonly encounterColumns = [
    { key: 'encounterDate', label: 'Ngày', sortable: true },
    { key: 'patientId', label: 'Bệnh nhân' },
    { key: 'encounterType', label: 'Loại' },
    { key: 'chiefComplaint', label: 'Lý do' },
    { key: 'status', label: 'Trạng thái' },
  ];
  readonly appointmentColumns = [
    { key: 'scheduledDate', label: 'Ngày', sortable: true },
    { key: 'startTime', label: 'Giờ', sortable: true },
    { key: 'patientId', label: 'Bệnh nhân' },
    { key: 'type', label: 'Loại' },
    { key: 'status', label: 'Trạng thái' },
  ];

  get encounterRows(): Record<string, unknown>[] {
    return this.recentEncounters.map(encounter => ({
      id: encounter.id,
      encounterDate: this.formatDate(encounter.encounterDate, true),
      patientId: `${encounter.patientId.slice(0, 8)}...`,
      encounterType: encounter.encounterTypeName || encounter.encounterType,
      chiefComplaint: encounter.chiefComplaint || '-',
      status: encounter.statusName || encounter.status,
      entity: encounter,
    }));
  }

  get appointmentRows(): Record<string, unknown>[] {
    return this.upcomingAppointments.map(appointment => ({
      id: appointment.id,
      scheduledDate: this.formatDate(appointment.scheduledDate, false),
      startTime: appointment.startTime,
      patientId: `${appointment.patientId.slice(0, 8)}...`,
      type: appointment.typeName || appointment.type,
      status: appointment.statusName || appointment.status,
      entity: appointment,
    }));
  }

  constructor(
    private authService: AuthService,
    private dashboardService: DashboardService,
    private patientService: PatientService,
    private appointmentService: AppointmentService,
    private clinicalService: ClinicalService,
    private router: Router,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        this.currentUserName = user?.fullName ?? 'User';
        this.cdr.markForCheck();
      });

    this.loadAllData();

    // Patient search
    this.patientSearchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$),
      )
      .subscribe((term) => {
        const query = (term ?? '').trim();
        if (query.length < 2) {
          this.patientSearchResults = [];
          this.cdr.markForCheck();
          return;
        }
        this.patientService.search(query, 1, 10)
          .pipe(takeUntil(this.destroy$))
          .subscribe((res) => {
            this.patientSearchResults = res.items;
            this.cdr.markForCheck();
          });
      });

    // Load recent patients from latest encounters
    this.patientService.search('', 1, 8)
      .pipe(takeUntil(this.destroy$))
      .subscribe((res) => {
        this.recentPatients = res.items;
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAllData(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      stats: this.dashboardService.getStats(),
      recent: this.dashboardService.getRecentEncounters(5),
      upcoming: this.dashboardService.getUpcomingAppointments(),
    })
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        this.stats = {
          ...data.stats,
          pendingLabs: data.stats.pendingLabs,
          outstandingInvoices: data.stats.outstandingInvoices,
          lowStockMedications: data.stats.lowStockMedications,
        };
        this.recentEncounters = data.recent.items || [];
        this.upcomingAppointments = data.upcoming.items || [];
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = 'Không thể tải dữ liệu tổng quan';
        this.loading = false;
        this.cdr.markForCheck();
      },
    });
  }

  displayPatientName(patient: Patient): string {
    return patient ? patient.fullName : '';
  }

  onPatientSelected(event: any): void {
    const patient: Patient = event.option.value;
    if (patient) {
      this.patientSearchControl.setValue('', { emitEvent: false });
      this.router.navigate(['/patients', patient.id, 'workspace']);
    }
  }

  openPatientWorkspace(id: string): void {
    this.router.navigate(['/patients', id, 'workspace']);
  }

  viewEncounter(id: string): void {
    this.router.navigate(['/clinical', id]);
  }

  encounterFromRow(row: Record<string, unknown>): Encounter { return row['entity'] as Encounter; }
  appointmentFromRow(row: Record<string, unknown>): Appointment { return row['entity'] as Appointment; }
  private formatDate(value: string | Date, includeTime: boolean): string {
    const options: Intl.DateTimeFormatOptions = includeTime
      ? { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }
      : { day: '2-digit', month: '2-digit' };
    return new Date(value).toLocaleDateString('vi-VN', options);
  }

  viewAppointment(id: string): void {
    this.router.navigate(['/appointments', id]);
  }
}
