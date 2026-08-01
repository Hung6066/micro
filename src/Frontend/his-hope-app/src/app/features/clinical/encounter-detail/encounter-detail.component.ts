import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, forkJoin, takeUntil } from 'rxjs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { ClinicalService } from '@core/services/clinical.service';
import { LabService } from '@core/services/lab.service';
import { PharmacyService } from '@core/services/pharmacy.service';
import { Encounter } from '@core/models/encounter.model';
import { LabOrder, LabTest } from '@core/models/lab-order.model';
import { Prescription } from '@core/models/prescription.model';
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

interface LabResultDisplay {
  testName: string;
  result: string;
  isAbnormal: boolean;
  statusCode: string;
  statusName: string;
}

@Component({
    selector: 'app-encounter-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule,
        MatIconModule, MatButtonModule, MatListModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent, HisHopePageSectionComponent,
        HisHopeStateComponent, HisHopeStatusBadgeComponent, HisHopeMetaItemComponent,
        HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'encounters.detail' | hhTranslate:'Chi tiết lượt khám'"
                      [subtitle]="encounter ? ('encounters.detailSubtitle' | hhTranslate:'Mã BN: {{patientId}} | Bác sĩ: {{providerId}} | {{date}}':{patientId: (encounter.patientId | slice:0:8), providerId: (encounter.providerId | slice:0:8), date: ((encounter.encounterDate | date:'dd/MM/yyyy HH:mm') || '')}) : ''">
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon> {{ 'common.back' | hhTranslate:'Quay lại' }}
        </button>
        @if (encounter) {
        <hh-status-badge [status]="encounter.status" [label]="encounter.statusName || encounter.status" />
        @if (encounter.status !== 'COMPLETED' && encounter.status !== 'SIGNED') {
        <button mat-stroked-button color="primary" (click)="createLabOrder()">
          <mat-icon>science</mat-icon> {{ 'encounters.orderLab' | hhTranslate:'Chỉ định XN' }}
        </button>
        }
        @if (encounter.status !== 'COMPLETED' && encounter.status !== 'SIGNED') {
        <button mat-stroked-button color="accent" (click)="createPrescription()">
          <mat-icon>medication</mat-icon> {{ 'encounters.prescribe' | hhTranslate:'Kê đơn' }}
        </button>
        }
        }
      </hh-page-header>

      @if (loading) {
      <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (error) {
      <hh-state kind="error" [message]="error">
        <button mat-stroked-button (click)="loadEncounter()">{{ 'common.retry' | hhTranslate }}</button>
      </hh-state>
      } @else if (encounter) {
      <!-- Overview -->
      <hh-page-section [title]="'encounters.section.overview' | hhTranslate:'Tổng quan'">
        <div class="meta-grid">
          <hh-meta-item [label]="'encounters.field.date' | hhTranslate:'Ngày khám'" [value]="(encounter.encounterDate | date:'medium') || '-'" icon="event" />
          <hh-meta-item [label]="'encounters.field.type' | hhTranslate:'Loại'" [value]="encounter.encounterTypeName || encounter.encounterType" icon="category" />
          <hh-meta-item [label]="'encounters.field.patient' | hhTranslate:'Bệnh nhân'" [value]="encounter.patientId" icon="person" />
          <hh-meta-item [label]="'encounters.field.provider' | hhTranslate:'Bác sĩ'" [value]="encounter.providerId" icon="medical_services" />
          @if (encounter.appointmentId) {
          <hh-meta-item [label]="'encounters.field.appointment' | hhTranslate:'Lịch hẹn'" [value]="encounter.appointmentId" icon="event_note" />
          }
        </div>
      </hh-page-section>

      <!-- SOAP Format -->
      <div class="soap-grid">
        <!-- S: Subjective -->
        <hh-page-section class="soap-card soap-s" [title]="'encounters.soap.subjective' | hhTranslate:'S · Subjective (Chủ quan)'">
          <div class="soap-section">
            <strong>{{ 'encounters.field.chiefComplaint' | hhTranslate:'Lý do khám (Chief Complaint)' }}:</strong>
            <p>{{ encounter.chiefComplaint || ('encounters.empty.chiefComplaint' | hhTranslate:'Chưa ghi nhận') }}</p>
          </div>
          @if (encounter.hpi) {
          <div class="soap-section">
            <strong>{{ 'encounters.field.hpi' | hhTranslate:'Bệnh sử (HPI)' }}:</strong>
            <div class="meta-grid">
              @if (encounter.hpi.onset) {
              <hh-meta-item [label]="'encounters.hpi.onset' | hhTranslate:'Khởi phát'" [value]="encounter.hpi.onset" />
              }
              @if (encounter.hpi.location) {
              <hh-meta-item [label]="'encounters.hpi.location' | hhTranslate:'Vị trí'" [value]="encounter.hpi.location" />
              }
              @if (encounter.hpi.duration) {
              <hh-meta-item [label]="'encounters.hpi.duration' | hhTranslate:'Thời gian'" [value]="encounter.hpi.duration" />
              }
              @if (encounter.hpi.characteristics) {
              <hh-meta-item [label]="'encounters.hpi.characteristics' | hhTranslate:'Tính chất'" [value]="encounter.hpi.characteristics" />
              }
              @if (encounter.hpi.aggravatingFactors) {
              <hh-meta-item [label]="'encounters.hpi.aggravating' | hhTranslate:'Yếu tố tăng nặng'" [value]="encounter.hpi.aggravatingFactors" />
              }
              @if (encounter.hpi.relievingFactors) {
              <hh-meta-item [label]="'encounters.hpi.relieving' | hhTranslate:'Yếu tố giảm nhẹ'" [value]="encounter.hpi.relievingFactors" />
              }
              @if (encounter.hpi.priorTreatments) {
              <hh-meta-item [label]="'encounters.hpi.priorTreatments' | hhTranslate:'Điều trị trước'" [value]="encounter.hpi.priorTreatments" />
              }
            </div>
          </div>
          }
        </hh-page-section>

        <!-- O: Objective -->
        <hh-page-section class="soap-card soap-o" [title]="'encounters.soap.objective' | hhTranslate:'O · Objective (Khách quan)'">
          @if (encounter.vitalSigns) {
          <div class="soap-section">
            <strong>{{ 'encounters.field.vitals' | hhTranslate:'Dấu hiệu sinh tồn' }}:</strong>
            <div class="meta-grid">
              @if (encounter.vitalSigns.temperature) {
              <hh-meta-item [label]="'encounters.vitals.temperature' | hhTranslate:'Nhiệt độ'" [value]="encounter.vitalSigns.temperature + '°C'" icon="thermostat" />
              }
              @if (encounter.vitalSigns.heartRate) {
              <hh-meta-item [label]="'encounters.vitals.heartRate' | hhTranslate:'Nhịp tim'" [value]="encounter.vitalSigns.heartRate + ' l/ph'" icon="monitor_heart" />
              }
              @if (encounter.vitalSigns.respiratoryRate) {
              <hh-meta-item [label]="'encounters.vitals.respiratoryRate' | hhTranslate:'Nhịp thở'" [value]="encounter.vitalSigns.respiratoryRate + ' /ph'" icon="air" />
              }
              @if (encounter.vitalSigns.systolicBP) {
              <hh-meta-item [label]="'encounters.vitals.bp' | hhTranslate:'Huyết áp'" [value]="encounter.vitalSigns.systolicBP + '/' + encounter.vitalSigns.diastolicBP" icon="bloodtype" />
              }
              @if (encounter.vitalSigns.oxygenSaturation) {
              <hh-meta-item [label]="'encounters.vitals.spo2' | hhTranslate:'SpO2'" [value]="encounter.vitalSigns.oxygenSaturation + '%'" icon="monitor_heart" />
              }
              @if (encounter.vitalSigns.weightKg) {
              <hh-meta-item [label]="'encounters.vitals.weight' | hhTranslate:'Cân nặng'" [value]="encounter.vitalSigns.weightKg + ' kg'" icon="monitor_weight" />
              }
              @if (encounter.vitalSigns.heightCm) {
              <hh-meta-item [label]="'encounters.vitals.height' | hhTranslate:'Chiều cao'" [value]="encounter.vitalSigns.heightCm + ' cm'" icon="straighten" />
              }
              @if (encounter.vitalSigns.bmi) {
              <hh-meta-item [label]="'encounters.vitals.bmi' | hhTranslate:'BMI'" [value]="encounter.vitalSigns.bmi + ''" icon="monitor_weight" />
              }
            </div>
          </div>
          } @else {
          <div class="soap-section">
            <p class="empty-section">{{ 'encounters.empty.vitals' | hhTranslate:'Chưa ghi nhận dấu hiệu sinh tồn' }}</p>
          </div>
          }

          <!-- Linked Lab Results -->
          @if (linkedLabResults.length > 0) {
          <div class="soap-section">
            <strong>{{ 'encounters.field.linkedLabs' | hhTranslate:'Kết quả xét nghiệm liên quan' }}:</strong>
            <mat-list dense>
              @for (lab of linkedLabResults; track lab.testName) {
              <mat-list-item>
                <mat-icon matListItemIcon>science</mat-icon>
                <span matListItemTitle>{{ lab.testName }}</span>
                <span matListItemLine>
                  {{ lab.result || ('encounters.empty.labResult' | hhTranslate:'Chưa có KQ') }}
                  @if (lab.isAbnormal) {
                  <span class="abnormal-badge">{{ 'encounters.abnormal' | hhTranslate:'Bất thường' }}</span>
                  }
                  <hh-status-badge [status]="lab.statusCode" [label]="lab.statusName" />
                </span>
              </mat-list-item>
              }
            </mat-list>
          </div>
          }
        </hh-page-section>

        <!-- A: Assessment -->
        <hh-page-section class="soap-card soap-a" [title]="'encounters.soap.assessment' | hhTranslate:'A · Assessment (Đánh giá)'">
          @if (encounter.diagnoses.length > 0) {
          <div>
            <mat-list>
              @for (d of encounter.diagnoses; track d.conditionName) {
              <mat-list-item>
                <mat-icon matListItemIcon [style.color]="d.isPrimary ? '#ffa726' : '#90a4ae'">
                  {{ d.isPrimary ? 'star' : 'label' }}
                </mat-icon>
                <span matListItemTitle>
                  {{ d.conditionName }}
                  @if (d.icd10Code) {
                  <small>({{ d.icd10Code }})</small>
                  }
                  <span class="diag-type">{{ d.isPrimary ? ('encounters.diag.primary' | hhTranslate:'Chính') : ('encounters.diag.secondary' | hhTranslate:'Phụ') }}</span>
                </span>
                @if (d.notes) {
                <span matListItemLine>{{ d.notes }}</span>
                }
              </mat-list-item>
              }
            </mat-list>
          </div>
          } @else {
          <p class="empty-section">{{ 'encounters.empty.diagnoses' | hhTranslate:'Chưa có chẩn đoán' }}</p>
          }
        </hh-page-section>

        <!-- P: Plan -->
        <hh-page-section class="soap-card soap-p" [title]="'encounters.soap.plan' | hhTranslate:'P · Plan (Kế hoạch)'">
          @if (encounter.assessment) {
          <div class="soap-section">
            <strong>{{ 'encounters.field.assessment' | hhTranslate:'Nhận định' }}:</strong>
            <p>{{ encounter.assessment }}</p>
          </div>
          }
          @if (encounter.plan) {
          <div class="soap-section">
            <strong>{{ 'encounters.field.plan' | hhTranslate:'Kế hoạch điều trị' }}:</strong>
            <p>{{ encounter.plan }}</p>
          </div>
          }
          @if (!encounter.assessment && !encounter.plan) {
          <div class="soap-section">
            <p class="empty-section">{{ 'encounters.empty.plan' | hhTranslate:'Chưa có kế hoạch điều trị' }}</p>
          </div>
          }

          <!-- Linked Prescriptions -->
          @if (linkedPrescriptions.length > 0) {
          <div class="soap-section">
            <strong>{{ 'encounters.field.linkedRx' | hhTranslate:'Đơn thuốc đã kê' }}:</strong>
            <mat-list dense>
              @for (rx of linkedPrescriptions; track rx.id) {
              <mat-list-item>
                <mat-icon matListItemIcon>medication</mat-icon>
                <span matListItemTitle>{{ rx.medicationName }}</span>
                <span matListItemLine>{{ rx.dosageInstructions }}</span>
              </mat-list-item>
              }
            </mat-list>
          </div>
          }
        </hh-page-section>
      </div>

      <!-- Timeline -->
      <hh-page-section [title]="'encounters.section.timeline' | hhTranslate:'Dòng thời gian'">
        <mat-list>
          <mat-list-item>
            <mat-icon matListItemIcon>add_circle</mat-icon>
            <div matListItemTitle>{{ 'encounters.timeline.created' | hhTranslate:'Khởi tạo' }}</div>
            <div matListItemLine>{{ encounter.createdAt | date:'medium' }}</div>
          </mat-list-item>
          @if (encounter.updatedAt) {
          <mat-list-item>
            <mat-icon matListItemIcon>update</mat-icon>
            <div matListItemTitle>{{ 'encounters.timeline.updated' | hhTranslate:'Cập nhật lần cuối' }}</div>
            <div matListItemLine>{{ encounter.updatedAt | date:'medium' }}</div>
          </mat-list-item>
          }
        </mat-list>
      </hh-page-section>
      }
    </hh-page-layout>
  `,
    styles: [`
    .meta-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }

    /* SOAP layout */
    .soap-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px; }
    .soap-card { border-top: 4px solid #e0e0e0; }
    .soap-s { border-top-color: #42a5f5; }
    .soap-o { border-top-color: #66bb6a; }
    .soap-a { border-top-color: #ffa726; }
    .soap-p { border-top-color: #ab47bc; }
    .soap-section { margin-bottom: 16px; }
    .soap-section:last-child { margin-bottom: 0; }
    .soap-section p { margin: 6px 0; line-height: 1.5; }
    .empty-section { color: #999; font-style: italic; }
    .diag-type { font-size: 11px; padding: 1px 8px; border-radius: 8px; background: #f5f5f5; margin-left: 6px; }
    .abnormal-badge { font-size: 10px; padding: 1px 6px; border-radius: 8px; background: #ffebee; color: #c62828; margin-left: 6px; }

    @media (max-width: 768px) {
      .soap-grid { grid-template-columns: 1fr; }
    }
  `],
})
export class EncounterDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);
  encounter?: Encounter;
  linkedLabResults: LabResultDisplay[] = [];
  linkedPrescriptions: Prescription[] = [];

  loading = true;
  error = '';

  constructor(
    private route: ActivatedRoute,
    private clinicalService: ClinicalService,
    private labService: LabService,
    private pharmacyService: PharmacyService,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadEncounter();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadEncounter(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.loading = true;
    this.error = '';
    this.clinicalService.getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: e => {
          this.encounter = e;
          this.loadLinkedData();
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = this.i18n.t('encounters.loadFailed', 'Không thể tải thông tin lượt khám.');
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  goBack(): void {
    this.router.navigate(['/clinical']);
  }

  private loadLinkedData(): void {
    if (!this.encounter) return;
    const patientId = this.encounter.patientId;

    forkJoin({
      labs: this.labService.getPatientLabOrders(patientId, 1, 100),
      prescriptions: this.pharmacyService.getPatientPrescriptions(patientId, 1, 100),
    })
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: (data) => {
        // Flatten lab orders into individual test results for display
        this.linkedLabResults = [];
        for (const order of data.labs.items || []) {
          for (const test of order.tests || []) {
            this.linkedLabResults.push({
              testName: test.testName,
              result: test.result?.value ?? '',
              isAbnormal: test.result?.abnormalFlagCode !== 'none' && test.result?.abnormalFlagCode !== undefined,
              statusCode: test.statusCode,
              statusName: test.statusName,
            });
          }
        }
        this.linkedPrescriptions = data.prescriptions.items || [];
        this.cdr.markForCheck();
      },
      error: () => {
        // Silently fail - linked data is supplementary
        this.cdr.markForCheck();
      },
    });
  }

  createLabOrder(): void {
    if (!this.encounter) return;
    this.router.navigate(['/labs/new'], {
      queryParams: {
        patientId: this.encounter.patientId,
        encounterId: this.encounter.id,
      },
    });
  }

  createPrescription(): void {
    if (!this.encounter) return;
    this.router.navigate(['/pharmacy/prescriptions/new'], {
      queryParams: {
        patientId: this.encounter.patientId,
        encounterId: this.encounter.id,
      },
    });
  }
}
