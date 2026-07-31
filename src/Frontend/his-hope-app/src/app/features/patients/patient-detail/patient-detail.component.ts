import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject, takeUntil } from 'rxjs';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';
import {
  HisHopeI18nService,
  HisHopeMetaItemComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageSectionComponent,
  HisHopeStateComponent,
  HisHopeStatusBadgeComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-patient-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatIconModule, MatButtonModule, MatListModule,
        MatTableModule, MatTabsModule, MatTooltipModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent, HisHopePageSectionComponent,
        HisHopeStateComponent, HisHopeStatusBadgeComponent, HisHopeMetaItemComponent,
        HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="patient?.fullName || ('patients.detail' | hhTranslate:'Hồ sơ bệnh nhân')"
                      [subtitle]="patient ? ('patients.detailSubtitle' | hhTranslate:'Mã BN: {{id}} | {{gender}} | Tuổi: {{age}}':{id: (patient.id | slice:0:8), gender: patient.genderName, age: patient.age}) : ''">
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon> {{ 'common.back' | hhTranslate:'Quay lại' }}
        </button>
        @if (patient) {
          @if (patient.isActive) {
          <button mat-raised-button color="accent" [routerLink]="['/patients', patient.id, 'edit']">
            <mat-icon>edit</mat-icon> {{ 'common.edit' | hhTranslate:'Chỉnh sửa' }}
          </button>
          }
          <button mat-stroked-button color="primary" [routerLink]="['/appointments/new']"
                  [queryParams]="{patientId: patient.id}">
            <mat-icon>calendar_today</mat-icon> {{ 'patients.newAppointment' | hhTranslate:'Đặt lịch hẹn' }}
          </button>
          <button mat-raised-button [color]="patient.isActive ? 'warn' : 'primary'"
                  (click)="toggleActive()">
            <mat-icon>{{ patient.isActive ? 'block' : 'check_circle' }}</mat-icon>
            {{ patient.isActive ? ('patients.deactivate' | hhTranslate:'Vô hiệu hóa') : ('patients.reactivate' | hhTranslate:'Kích hoạt lại') }}
          </button>
        }
      </hh-page-header>

      @if (loading) {
      <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (error) {
      <hh-state kind="error" [message]="error">
        <button mat-stroked-button (click)="loadPatient()">{{ 'common.retry' | hhTranslate }}</button>
      </hh-state>
      } @else if (patient) {
      <mat-tab-group dynamicHeight>
        <!-- Tab 1: Thông tin -->
        <mat-tab [label]="'patients.tab.info' | hhTranslate:'Thông tin'">
          <div class="tab-content">
            <hh-page-section [title]="'patients.section.personal' | hhTranslate:'Thông tin cá nhân'">
              <div class="meta-grid">
                <hh-meta-item [label]="'patients.field.dob' | hhTranslate:'Ngày sinh'" [value]="(patient.dateOfBirth | date:'mediumDate') || '-'" icon="cake" />
                <hh-meta-item [label]="'patients.field.gender' | hhTranslate:'Giới tính'" [value]="patient.genderName" icon="person" />
                <hh-meta-item [label]="'patients.field.bloodType' | hhTranslate:'Nhóm máu'" [value]="patient.bloodTypeName || '-'" icon="bloodtype" />
                <hh-meta-item [label]="'patients.field.nationalId' | hhTranslate:'CMND/CCCD'" [value]="patient.nationalId || '-'" icon="badge" />
                <hh-meta-item [label]="'patients.field.occupation' | hhTranslate:'Nghề nghiệp'" [value]="patient.occupation || '-'" icon="work" />
              </div>
            </hh-page-section>

            <hh-page-section [title]="'patients.section.contact' | hhTranslate:'Liên hệ'">
              <div class="meta-grid">
                <hh-meta-item [label]="'patients.field.phone' | hhTranslate:'Điện thoại'" [value]="patient.phone" icon="phone" />
                <hh-meta-item [label]="'patients.field.email' | hhTranslate:'Email'" [value]="patient.email || '-'" icon="email" />
                <hh-meta-item [label]="'patients.field.address' | hhTranslate:'Địa chỉ'" [value]="patient.street + ', ' + patient.district + ', ' + patient.city" icon="home" />
                <hh-meta-item [label]="'patients.field.insurance' | hhTranslate:'Bảo hiểm'" [value]="patient.insuranceId || '-'" icon="health_and_safety" />
                <hh-meta-item [label]="'patients.field.emergency' | hhTranslate:'Liên hệ khẩn cấp'" [value]="(patient.emergencyContactName || '-') + ' - ' + (patient.emergencyContactPhone || '-')" icon="emergency" />
              </div>
            </hh-page-section>

            <hh-page-section [title]="'patients.section.conditions' | hhTranslate:'Bệnh lý ({{count}})':{count: patient.conditions.length}">
              @if (patient.conditions.length > 0) {
              <mat-list>
                @for (c of patient.conditions; track c) {
                <mat-list-item>
                  <mat-icon matListItemIcon>info</mat-icon>
                  <span matListItemTitle>{{ c.conditionName }} @if (c.icd10Code) {<small>({{ c.icd10Code }})</small>}</span>
                  <span matListItemLine>{{ c.isChronic ? ('patients.chronic' | hhTranslate:'Mạn tính') : ('patients.acute' | hhTranslate:'Cấp tính') }} | {{ c.isActive ? ('patients.active' | hhTranslate:'Đang hoạt động') : ('patients.resolved' | hhTranslate:'Đã khỏi') }}</span>
                </mat-list-item>
                }
              </mat-list>
              } @else {
              <p class="empty">{{ 'patients.noConditions' | hhTranslate:'Không có bệnh lý nào' }}</p>
              }
            </hh-page-section>

            <hh-page-section [title]="'patients.section.allergies' | hhTranslate:'Dị ứng ({{count}})':{count: patient.allergies.length}">
              @if (patient.allergies.length > 0) {
              <mat-list>
                @for (a of patient.allergies; track a) {
                <mat-list-item>
                  <mat-icon matListItemIcon>warning</mat-icon>
                  <span matListItemTitle>{{ a.allergen }}</span>
                  <span matListItemLine>{{ a.reaction || ('patients.unknownReaction' | hhTranslate:'Không rõ phản ứng') }} | {{ a.severity || 'N/A' }}</span>
                </mat-list-item>
                }
              </mat-list>
              } @else {
              <p class="empty">{{ 'patients.noAllergies' | hhTranslate:'Không có dị ứng nào' }}</p>
              }
            </hh-page-section>
          </div>
        </mat-tab>

        <!-- Tab 2: Lịch sử khám -->
        <mat-tab [label]="'patients.tab.encounters' | hhTranslate:'Lịch sử khám ({{count}})':{count: encounters.length}">
          <div class="tab-content">
            @if (loadingEncounters) {
            <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
            }
            @if (!loadingEncounters && encounters.length === 0) {
            <hh-state kind="empty" [message]="'patients.noEncounters' | hhTranslate:'Bệnh nhân chưa có lượt khám nào'" />
            }
            @if (!loadingEncounters && encounters.length > 0) {
            <table mat-table [dataSource]="encounters" class="records-table">
              <ng-container matColumnDef="encounterDate">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.encounterDate' | hhTranslate:'Ngày khám' }}</th>
                <td mat-cell *matCellDef="let e">{{ e.encounterDate | date:'dd/MM/yyyy HH:mm' }}</td>
              </ng-container>
              <ng-container matColumnDef="encounterType">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.type' | hhTranslate:'Loại' }}</th>
                <td mat-cell *matCellDef="let e">{{ e.encounterTypeName || e.encounterType }}</td>
              </ng-container>
              <ng-container matColumnDef="chiefComplaint">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.chiefComplaint' | hhTranslate:'Lý do khám' }}</th>
                <td mat-cell *matCellDef="let e">{{ e.chiefComplaint || '-' }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | hhTranslate:'Trạng thái' }}</th>
                <td mat-cell *matCellDef="let e">
                  <hh-status-badge [status]="e.status" [label]="e.statusName || e.status" />
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let e">
                  <button mat-icon-button [routerLink]="['/clinical', e.id]" [matTooltip]="'common.details' | hhTranslate:'Xem chi tiết'"
                          [attr.aria-label]="'common.details' | hhTranslate:'Xem chi tiết'">
                    <mat-icon>visibility</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['encounterDate','encounterType','chiefComplaint','status','actions']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['encounterDate','encounterType','chiefComplaint','status','actions'];"></tr>
            </table>
            }
          </div>
        </mat-tab>

        <!-- Tab 3: Đơn thuốc -->
        <mat-tab [label]="'patients.tab.prescriptions' | hhTranslate:'Đơn thuốc ({{count}})':{count: prescriptions.length}">
          <div class="tab-content">
            @if (loadingPrescriptions) {
            <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
            }
            @if (!loadingPrescriptions && prescriptions.length === 0) {
            <hh-state kind="empty" [message]="'patients.noPrescriptions' | hhTranslate:'Bệnh nhân chưa có đơn thuốc nào'" />
            }
            @if (!loadingPrescriptions && prescriptions.length > 0) {
            <table mat-table [dataSource]="prescriptions" class="records-table">
              <ng-container matColumnDef="prescribedDate">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.prescribedDate' | hhTranslate:'Ngày kê' }}</th>
                <td mat-cell *matCellDef="let p">{{ p.prescribedDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="medicationName">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.medication' | hhTranslate:'Thuốc' }}</th>
                <td mat-cell *matCellDef="let p">{{ p.medicationName }}</td>
              </ng-container>
              <ng-container matColumnDef="dosage">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.dosage' | hhTranslate:'Liều dùng' }}</th>
                <td mat-cell *matCellDef="let p">{{ p.dosage }}</td>
              </ng-container>
              <ng-container matColumnDef="frequency">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.frequency' | hhTranslate:'Tần suất' }}</th>
                <td mat-cell *matCellDef="let p">{{ p.frequency }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | hhTranslate:'Trạng thái' }}</th>
                <td mat-cell *matCellDef="let p">
                  <hh-status-badge [status]="p.status" [label]="p.statusName || p.status" />
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['prescribedDate','medicationName','dosage','frequency','status']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['prescribedDate','medicationName','dosage','frequency','status'];"></tr>
            </table>
            }
          </div>
        </mat-tab>

        <!-- Tab 4: Xét nghiệm -->
        <mat-tab [label]="'patients.tab.labOrders' | hhTranslate:'Xét nghiệm ({{count}})':{count: labOrders.length}">
          <div class="tab-content">
            @if (loadingLabs) {
            <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
            }
            @if (!loadingLabs && labOrders.length === 0) {
            <hh-state kind="empty" [message]="'patients.noLabOrders' | hhTranslate:'Bệnh nhân chưa có xét nghiệm nào'" />
            }
            @if (!loadingLabs && labOrders.length > 0) {
            <table mat-table [dataSource]="labOrders" class="records-table">
              <ng-container matColumnDef="orderDate">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.orderDate' | hhTranslate:'Ngày chỉ định' }}</th>
                <td mat-cell *matCellDef="let l">{{ l.orderDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="testName">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.test' | hhTranslate:'Xét nghiệm' }}</th>
                <td mat-cell *matCellDef="let l">{{ l.testName }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>{{ 'common.status' | hhTranslate:'Trạng thái' }}</th>
                <td mat-cell *matCellDef="let l">
                  <hh-status-badge [status]="l.status" [label]="l.statusName || l.status" />
                </td>
              </ng-container>
              <ng-container matColumnDef="result">
                <th mat-header-cell *matHeaderCellDef>{{ 'patients.column.result' | hhTranslate:'Kết quả' }}</th>
                <td mat-cell *matCellDef="let l">{{ l.result || '-' }}</td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let l">
                  <button mat-icon-button [routerLink]="['/labs', l.id]" [matTooltip]="'common.details' | hhTranslate:'Xem chi tiết'"
                          [attr.aria-label]="'common.details' | hhTranslate:'Xem chi tiết'">
                    <mat-icon>visibility</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['orderDate','testName','status','result','actions']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['orderDate','testName','status','result','actions'];"></tr>
            </table>
            }
          </div>
        </mat-tab>
      </mat-tab-group>
      }
    </hh-page-layout>
  `,
    styles: [`
    .tab-content {
      padding: 20px 0;
    }

    .meta-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }

    .empty {
      color: var(--text-muted, #A1A09B);
      font-style: italic;
      padding: 12px;
    }

    .records-table {
      width: 100%;
    }

    ::ng-deep .mat-mdc-tab-body-content { overflow: hidden; }
  `],
})
export class PatientDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);
  patient?: Patient;

  encounters: Encounter[] = [];
  prescriptions: Prescription[] = [];
  labOrders: LabOrder[] = [];

  loading = true;
  error = '';
  loadingEncounters = false;
  loadingPrescriptions = false;
  loadingLabs = false;

  constructor(
    private route: ActivatedRoute,
    private patientService: PatientService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadPatient();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadPatient(): void {
    const id = this.route.snapshot.params['id'];
    this.loading = true;
    this.error = '';
    this.patientService.getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (p) => {
          this.patient = p;
          this.loadTabData();
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t('patients.loadFailed', 'Không thể tải thông tin bệnh nhân.');
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  goBack(): void {
    this.router.navigate(['/patients']);
  }

  private loadTabData(): void {
    if (!this.patient) return;
    const patientId = this.patient.id;

    // Load encounters
    this.loadingEncounters = true;
    this.patientService.getEncounters(patientId, 1, 10)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.encounters = res.items; this.loadingEncounters = false; this.cdr.markForCheck(); },
        error: () => { this.loadingEncounters = false; this.cdr.markForCheck(); },
      });

    // Load prescriptions
    this.loadingPrescriptions = true;
    this.patientService.getPrescriptions(patientId, 1, 10)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.prescriptions = res.items; this.loadingPrescriptions = false; this.cdr.markForCheck(); },
        error: () => { this.loadingPrescriptions = false; this.cdr.markForCheck(); },
      });

    // Load lab orders
    this.loadingLabs = true;
    this.patientService.getLabOrders(patientId, 1, 10)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (res) => { this.labOrders = res.items; this.loadingLabs = false; this.cdr.markForCheck(); },
        error: () => { this.loadingLabs = false; this.cdr.markForCheck(); },
      });
  }

  toggleActive(): void {
    if (!this.patient) return;
    const id = this.patient.id;
    const action = this.patient.isActive ? 'vô hiệu hóa' : 'kích hoạt lại';
    const obs = this.patient.isActive
      ? this.patientService.deactivate(id)
      : this.patientService.reactivate(id);

    obs.pipe(takeUntil(this.destroy$)).subscribe({
      next: () => {
        this.patient!.isActive = !this.patient!.isActive;
        this.snackBar.open(`Đã ${action} bệnh nhân`, 'Đóng', { duration: 3000 });
        this.cdr.markForCheck();
      },
      error: () => {
        this.snackBar.open(`Không thể ${action} bệnh nhân`, 'Đóng', { duration: 5000 });
        this.cdr.markForCheck();
      },
    });
  }
}
