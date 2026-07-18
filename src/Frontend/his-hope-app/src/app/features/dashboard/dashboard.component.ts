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

@Component({
    selector: 'app-dashboard',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatCardModule, MatInputModule, MatFormFieldModule, MatIconModule, MatButtonModule,
        MatProgressSpinnerModule, MatTableModule, MatChipsModule, MatAutocompleteModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="dashboard">
      <div class="welcome-section">
        <div>
          <h1>Xin chào, {{ currentUserName }}</h1>
          <p class="page-subtitle">Tổng quan bệnh viện</p>
        </div>
        <div class="date-display">
          <mat-icon>calendar_today</mat-icon>
          <span>{{ today | date:'EEEE, dd/MM/yyyy' }}</span>
        </div>
      </div>

      @if (loading) {
      <div class="loading-state" aria-live="polite" aria-busy="true">
        <mat-spinner diameter="40"></mat-spinner>
        <span class="sr-only">Đang tải dữ liệu...</span>
      </div>
      }

      @if (error) {
      <div class="error-message" role="alert" aria-live="assertive">
        <mat-icon aria-hidden="true">error_outline</mat-icon>
        <span>{{ error }}</span>
      </div>
      }

      <!-- Row 1: Stat Cards — bento-grid layout -->
      @if (!loading && !error) {
      <div class="stats-bento">
        <mat-card class="stat-card card-patients">
          <mat-card-content>
            <div class="stat-icon"><mat-icon>people</mat-icon></div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.totalPatients }}</span>
              <span class="stat-label">Tổng bệnh nhân</span>
              @if (stats.newPatientsToday > 0) {
              <span class="stat-trend">
                +{{ stats.newPatientsToday }} hôm nay
              </span>
              }
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card card-appointments">
          <mat-card-content>
            <div class="stat-icon"><mat-icon>calendar_today</mat-icon></div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.todayAppointments }}</span>
              <span class="stat-label">Lịch hẹn hôm nay</span>
              @if (stats.appointmentsTomorrow > 0) {
              <span class="stat-trend">
                {{ stats.appointmentsTomorrow }} ngày mai
              </span>
              }
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card card-encounters">
          <mat-card-content>
            <div class="stat-icon"><mat-icon>emergency</mat-icon></div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.activeEncounters }}</span>
              <span class="stat-label">Điều trị đang mở</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card card-labs">
          <mat-card-content>
            <div class="stat-icon"><mat-icon>science</mat-icon></div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.pendingLabs }}</span>
              <span class="stat-label">Xét nghiệm chờ KQ</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card card-billing">
          <mat-card-content>
            <div class="stat-icon"><mat-icon>receipt</mat-icon></div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.outstandingInvoices }}</span>
              <span class="stat-label">Hóa đơn chưa TT</span>
            </div>
          </mat-card-content>
        </mat-card>

        <mat-card class="stat-card card-pharmacy">
          <mat-card-content>
            <div class="stat-icon"><mat-icon>medication</mat-icon></div>
            <div class="stat-info">
              <span class="stat-value">{{ stats.lowStockMedications }}</span>
              <span class="stat-label">Thuốc sắp hết</span>
            </div>
          </mat-card-content>
        </mat-card>
      </div>
      }

      <!-- Patient Quick Search -->
      @if (!loading && !error) {
      <mat-card class="section-card search-card">
        <mat-card-header>
          <mat-card-title>
            <mat-icon>search</mat-icon> Tìm bệnh nhân
          </mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-form-field appearance="outline" class="dashboard-search">
            <mat-label>Nhập tên, số điện thoại hoặc mã bệnh nhân...</mat-label>
            <input matInput [formControl]="patientSearchControl" [matAutocomplete]="auto">
            <mat-icon matSuffix>search</mat-icon>
          </mat-form-field>
          <mat-autocomplete #auto="matAutocomplete" [displayWith]="displayPatientName"
                            (optionSelected)="onPatientSelected($event)">
            @for (p of patientSearchResults; track p.id) {
            <mat-option [value]="p">
              <div class="search-result-item">
                <span class="result-name">{{ p.fullName }}</span>
                <span class="result-meta">{{ p.genderName }} · {{ p.age }} tuổi · {{ p.phone }}</span>
              </div>
            </mat-option>
            }
            @if (patientSearchResults.length === 0 && (patientSearchControl.value?.length ?? 0) >= 2) {
            <mat-option disabled>
              <span class="no-results">Không tìm thấy bệnh nhân</span>
            </mat-option>
            }
          </mat-autocomplete>
        </mat-card-content>
      </mat-card>
      }

      <!-- Recent Patients -->
      @if (!loading && !error && recentPatients.length > 0) {
      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>
            <mat-icon>history</mat-icon> Bệnh nhân gần đây
          </mat-card-title>
          <button mat-stroked-button size="small" routerLink="/patients">Xem tất cả</button>
        </mat-card-header>
        <mat-card-content>
          <div class="recent-patients-grid">
            @for (p of recentPatients; track p.id) {
            <div class="recent-patient-card" (click)="openPatientWorkspace(p.id)">
              <div class="rp-avatar">{{ p.fullName.charAt(0) }}</div>
              <div class="rp-info">
                <span class="rp-name">{{ p.fullName }}</span>
                <span class="rp-meta">{{ p.genderName }} · {{ p.age }} tuổi</span>
              </div>
              <mat-icon class="rp-arrow">chevron_right</mat-icon>
            </div>
            }
          </div>
        </mat-card-content>
      </mat-card>
      }

      <!-- Row 2: Recent Encounters -->
      @if (!loading && !error) {
      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>
            <mat-icon>history</mat-icon> Lượt khám gần đây
          </mat-card-title>
          <button mat-stroked-button size="small" routerLink="/clinical">Xem tất cả</button>
        </mat-card-header>
        <mat-card-content>
          @if (recentEncounters.length === 0) {
          <div class="section-empty">Chưa có lượt khám nào</div>
          }
          @if (recentEncounters.length > 0) {
          <table mat-table [dataSource]="recentEncounters" class="dashboard-table">
            <ng-container matColumnDef="encounterDate">
              <th mat-header-cell *matHeaderCellDef>Ngày</th>
              <td mat-cell *matCellDef="let e">{{ e.encounterDate | date:'dd/MM HH:mm' }}</td>
            </ng-container>
            <ng-container matColumnDef="patientId">
              <th mat-header-cell *matHeaderCellDef>Bệnh nhân</th>
              <td mat-cell *matCellDef="let e">{{ e.patientId | slice:0:8 }}...</td>
            </ng-container>
            <ng-container matColumnDef="encounterType">
              <th mat-header-cell *matHeaderCellDef>Loại</th>
              <td mat-cell *matCellDef="let e">{{ e.encounterTypeName || e.encounterType }}</td>
            </ng-container>
            <ng-container matColumnDef="chiefComplaint">
              <th mat-header-cell *matHeaderCellDef>Lý do</th>
              <td mat-cell *matCellDef="let e">{{ e.chiefComplaint || '-' }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
              <td mat-cell *matCellDef="let e">
                <span class="status-badge" [class]="'status-' + e.status.toLowerCase()">{{ e.statusName || e.status }}</span>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['encounterDate','patientId','encounterType','chiefComplaint','status']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['encounterDate','patientId','encounterType','chiefComplaint','status'];" class="clickable-row" (click)="viewEncounter(row.id)"></tr>
          </table>
          }
        </mat-card-content>
      </mat-card>
      }

      <!-- Row 3: Upcoming Appointments -->
      @if (!loading && !error) {
      <mat-card class="section-card">
        <mat-card-header>
          <mat-card-title>
            <mat-icon>upcoming</mat-icon> Lịch hẹn sắp tới
          </mat-card-title>
          <button mat-stroked-button size="small" routerLink="/appointments">Xem tất cả</button>
        </mat-card-header>
        <mat-card-content>
          @if (upcomingAppointments.length === 0) {
          <div class="section-empty">Không có lịch hẹn nào</div>
          }
          @if (upcomingAppointments.length > 0) {
          <table mat-table [dataSource]="upcomingAppointments" class="dashboard-table">
            <ng-container matColumnDef="scheduledDate">
              <th mat-header-cell *matHeaderCellDef>Ngày</th>
              <td mat-cell *matCellDef="let a">{{ a.scheduledDate | date:'dd/MM' }}</td>
            </ng-container>
            <ng-container matColumnDef="startTime">
              <th mat-header-cell *matHeaderCellDef>Giờ</th>
              <td mat-cell *matCellDef="let a">{{ a.startTime }}</td>
            </ng-container>
            <ng-container matColumnDef="patientId">
              <th mat-header-cell *matHeaderCellDef>Bệnh nhân</th>
              <td mat-cell *matCellDef="let a">{{ a.patientId | slice:0:8 }}...</td>
            </ng-container>
            <ng-container matColumnDef="type">
              <th mat-header-cell *matHeaderCellDef>Loại</th>
              <td mat-cell *matCellDef="let a">{{ a.typeName || a.type }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
              <td mat-cell *matCellDef="let a">
                <span class="status-badge" [class]="'apt-status-' + a.status.toLowerCase()">{{ a.statusName || a.status }}</span>
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="['scheduledDate','startTime','patientId','type','status']"></tr>
            <tr mat-row *matRowDef="let row; columns: ['scheduledDate','startTime','patientId','type','status'];" class="clickable-row" (click)="viewAppointment(row.id)"></tr>
          </table>
          }
        </mat-card-content>
      </mat-card>
      }
    </div>
  `,
    styles: [`
    .dashboard {
      max-width: var(--max-width-container, 1200px);
      margin: 0 auto;
      padding: 32px 24px;
    }

    .welcome-section {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 32px;
      flex-wrap: wrap;
      gap: 12px;
    }

    .welcome-section h1 {
      font-size: 26px;
      font-weight: 600;
      letter-spacing: -0.01em;
      line-height: 1.3;
    }

    .date-display {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--text-secondary, #787774);
      font-size: 14px;
      padding: 8px 16px;
      background: var(--surface-white, #FFFFFF);
      border: 1px solid var(--border-default, #EAEAEA);
      border-radius: var(--radius-card, 8px);
    }

    /* ── Bento Grid ── */
    .stats-bento {
      display: grid;
      grid-template-columns: 2fr 1fr 1fr;
      grid-auto-rows: auto;
      gap: 16px;
      margin-bottom: 32px;
    }

    .stat-card {
      cursor: default;
      transition: transform 150ms ease;
      animation: fadeInUp 400ms ease forwards;
    }

    .stat-card:hover {
      transform: translateY(-1px);
    }

    .stat-card mat-card-content {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 20px;
    }

    .stat-icon {
      width: 44px;
      height: 44px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .stat-icon mat-icon {
      font-size: 22px;
      width: 22px;
      height: 22px;
    }

    .card-patients .stat-icon { background: var(--pastel-blue, #E1F3FE); }
    .card-patients .stat-icon mat-icon { color: var(--pastel-blue-text, #1A6BB5); }

    .card-appointments .stat-icon { background: var(--pastel-green, #EDF3EC); }
    .card-appointments .stat-icon mat-icon { color: var(--pastel-green-text, #2F6B4A); }

    .card-encounters .stat-icon { background: var(--pastel-red, #FDEBEC); }
    .card-encounters .stat-icon mat-icon { color: var(--pastel-red-text, #C25450); }

    .card-labs .stat-icon { background: var(--pastel-purple, #F0ECFA); }
    .card-labs .stat-icon mat-icon { color: var(--pastel-purple-text, #6B4FA0); }

    .card-billing .stat-icon { background: var(--pastel-yellow, #FBF3DB); }
    .card-billing .stat-icon mat-icon { color: var(--pastel-yellow-text, #8D6E2B); }

    .card-pharmacy .stat-icon { background: var(--pastel-orange, #FEF0E0); }
    .card-pharmacy .stat-icon mat-icon { color: var(--pastel-orange-text, #B6581C); }

    /* Make first card span wider */
    .card-patients { grid-column: span 1; }
    .card-appointments { grid-column: span 1; }

    .stat-info {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .stat-value {
      font-size: 30px;
      font-weight: 600;
      line-height: 1.1;
      font-variant-numeric: tabular-nums;
      color: var(--text-primary, #1A1A1A);
    }

    .stat-label {
      color: var(--text-secondary, #787774);
      font-size: 11px;
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-top: 4px;
    }

    .stat-trend {
      color: var(--color-primary, #2F6B4A);
      font-size: 12px;
      margin-top: 4px;
      font-weight: 500;
    }

    /* ── Search Card ── */
    .search-card mat-card-content {
      padding: 16px 20px;
    }

    .dashboard-search {
      width: 100%;
      max-width: 600px;
    }

    .search-result-item {
      display: flex;
      flex-direction: column;
      padding: 4px 0;
    }

    .result-name {
      font-weight: 500;
      font-size: 13px;
    }

    .result-meta {
      font-size: 11px;
      color: var(--text-secondary, #787774);
    }

    .no-results {
      color: var(--text-muted, #A1A09B);
      font-style: italic;
    }

    /* ── Recent Patients ── */
    .recent-patients-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 12px;
    }

    .recent-patient-card {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 16px;
      border-radius: var(--radius-card, 8px);
      cursor: pointer;
      transition: background 150ms ease;
      border: 1px solid var(--border-light, #F0F0EE);
    }

    .recent-patient-card:hover {
      background: rgba(0, 0, 0, 0.02);
    }

    .rp-avatar {
      width: 40px;
      height: 40px;
      border-radius: 8px;
      background: var(--pastel-green, #EDF3EC);
      color: var(--color-primary, #2F6B4A);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 16px;
      font-weight: 600;
      flex-shrink: 0;
    }

    .rp-info {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .rp-name {
      font-weight: 500;
      font-size: 14px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .rp-meta {
      font-size: 12px;
      color: var(--text-secondary, #787774);
    }

    .rp-arrow {
      color: var(--text-muted, #A1A09B);
    }

    /* ── Section Cards ── */
    .section-card {
      margin-bottom: 24px;
    }

    .section-card mat-card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .section-card mat-card-title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 16px;
      font-weight: 600;
    }

    .section-empty {
      color: var(--text-muted, #A1A09B);
      text-align: center;
      padding: 32px;
      font-style: italic;
    }

    /* ── Tables ── */
    .dashboard-table {
      width: 100%;
    }

    .clickable-row {
      cursor: pointer;
      transition: background-color 150ms ease;
    }

    .clickable-row:hover {
      background: rgba(0, 0, 0, 0.02);
    }

    /* ── Responsive ── */
    @media (max-width: 960px) {
      .stats-bento {
        grid-template-columns: 1fr 1fr;
      }
    }

    @media (max-width: 599px) {
      .dashboard {
        padding: 20px 12px;
      }

      .stats-bento {
        grid-template-columns: 1fr 1fr;
        gap: 12px;
      }

      .stat-value {
        font-size: 24px;
      }
    }
  `],
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

  private loadAllData(): void {
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

  viewAppointment(id: string): void {
    this.router.navigate(['/appointments', id]);
  }
}
