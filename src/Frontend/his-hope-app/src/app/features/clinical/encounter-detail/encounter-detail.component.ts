import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, forkJoin, takeUntil } from 'rxjs';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatListModule } from '@angular/material/list';
import { ClinicalService } from '@core/services/clinical.service';
import { LabService } from '@core/services/lab.service';
import { PharmacyService } from '@core/services/pharmacy.service';
import { Encounter } from '@core/models/encounter.model';
import { LabOrder, LabTest } from '@core/models/lab-order.model';
import { Prescription } from '@core/models/prescription.model';

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
        MatCardModule, MatIconModule, MatButtonModule, MatListModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    @if (encounter) {
    <div class="detail">
      <div class="header">
        <div>
          <h1>Chi tiết lượt khám</h1>
          <p class="header-sub">
            Mã BN: {{ encounter.patientId | slice:0:8 }}... | Bác sĩ: {{ encounter.providerId | slice:0:8 }}...
            | {{ encounter.encounterDate | date:'dd/MM/yyyy HH:mm' }}
          </p>
        </div>
        <div class="header-actions">
          <div class="status-badge" [class]="'status-' + encounter.status.toLowerCase()">
            {{ encounter.statusName || encounter.status }}
          </div>
          @if (encounter.status !== 'COMPLETED' && encounter.status !== 'SIGNED') {
          <button mat-stroked-button color="primary" (click)="createLabOrder()">
            <mat-icon>science</mat-icon> Chỉ định XN
          </button>
          }
          @if (encounter.status !== 'COMPLETED' && encounter.status !== 'SIGNED') {
          <button mat-stroked-button color="accent" (click)="createPrescription()">
            <mat-icon>medication</mat-icon> Kê đơn
          </button>
          }
        </div>
      </div>

      <!-- Overview -->
      <mat-card class="overview-card">
        <mat-card-header><mat-card-title><mat-icon>info</mat-icon> Tổng quan</mat-card-title></mat-card-header>
        <mat-card-content>
          <div class="overview-grid">
            <div><strong>Ngày khám:</strong> {{ encounter.encounterDate | date:'medium' }}</div>
            <div><strong>Loại:</strong> {{ encounter.encounterTypeName || encounter.encounterType }}</div>
            <div><strong>Bệnh nhân:</strong> {{ encounter.patientId }}</div>
            <div><strong>Bác sĩ:</strong> {{ encounter.providerId }}</div>
            @if (encounter.appointmentId) {
            <div><strong>Lịch hẹn:</strong> {{ encounter.appointmentId }}</div>
            }
          </div>
        </mat-card-content>
      </mat-card>

      <!-- SOAP Format -->
      <div class="soap-grid">
        <!-- S: Subjective -->
        <mat-card class="soap-card soap-s">
          <mat-card-header>
            <mat-card-title><span class="soap-label">S</span> Subjective (Chủ quan)</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div class="soap-section">
              <strong>Lý do khám (Chief Complaint):</strong>
              <p>{{ encounter.chiefComplaint || 'Chưa ghi nhận' }}</p>
            </div>
            @if (encounter.hpi) {
            <div class="soap-section">
              <strong>Bệnh sử (HPI):</strong>
              <div class="hpi-grid">
                @if (encounter.hpi.onset) {
                <div><strong>Khởi phát:</strong> {{ encounter.hpi.onset }}</div>
                }
                @if (encounter.hpi.location) {
                <div><strong>Vị trí:</strong> {{ encounter.hpi.location }}</div>
                }
                @if (encounter.hpi.duration) {
                <div><strong>Thời gian:</strong> {{ encounter.hpi.duration }}</div>
                }
                @if (encounter.hpi.characteristics) {
                <div><strong>Tính chất:</strong> {{ encounter.hpi.characteristics }}</div>
                }
                @if (encounter.hpi.aggravatingFactors) {
                <div><strong>Yếu tố tăng nặng:</strong> {{ encounter.hpi.aggravatingFactors }}</div>
                }
                @if (encounter.hpi.relievingFactors) {
                <div><strong>Yếu tố giảm nhẹ:</strong> {{ encounter.hpi.relievingFactors }}</div>
                }
                @if (encounter.hpi.priorTreatments) {
                <div><strong>Điều trị trước:</strong> {{ encounter.hpi.priorTreatments }}</div>
                }
              </div>
            </div>
            }
          </mat-card-content>
        </mat-card>

        <!-- O: Objective -->
        <mat-card class="soap-card soap-o">
          <mat-card-header>
            <mat-card-title><span class="soap-label">O</span> Objective (Khách quan)</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (encounter.vitalSigns) {
            <div class="soap-section">
              <strong>Dấu hiệu sinh tồn:</strong>
              <div class="vitals-grid">
                @if (encounter.vitalSigns.temperature) {
                <div><strong>Nhiệt độ:</strong> {{ encounter.vitalSigns.temperature }}°C</div>
                }
                @if (encounter.vitalSigns.heartRate) {
                <div><strong>Nhịp tim:</strong> {{ encounter.vitalSigns.heartRate }} l/ph</div>
                }
                @if (encounter.vitalSigns.respiratoryRate) {
                <div><strong>Nhịp thở:</strong> {{ encounter.vitalSigns.respiratoryRate }} /ph</div>
                }
                @if (encounter.vitalSigns.systolicBP) {
                <div><strong>HA:</strong> {{ encounter.vitalSigns.systolicBP }}/{{ encounter.vitalSigns.diastolicBP }}</div>
                }
                @if (encounter.vitalSigns.oxygenSaturation) {
                <div><strong>SpO2:</strong> {{ encounter.vitalSigns.oxygenSaturation }}%</div>
                }
                @if (encounter.vitalSigns.weightKg) {
                <div><strong>Cân nặng:</strong> {{ encounter.vitalSigns.weightKg }} kg</div>
                }
                @if (encounter.vitalSigns.heightCm) {
                <div><strong>Chiều cao:</strong> {{ encounter.vitalSigns.heightCm }} cm</div>
                }
                @if (encounter.vitalSigns.bmi) {
                <div><strong>BMI:</strong> {{ encounter.vitalSigns.bmi }}</div>
                }
              </div>
            </div>
            }
            @if (!encounter.vitalSigns) {
            <div class="soap-section">
              <p class="empty-section">Chưa ghi nhận dấu hiệu sinh tồn</p>
            </div>
            }

            <!-- Linked Lab Results -->
            @if (linkedLabResults.length > 0) {
            <div class="soap-section">
              <strong>Kết quả xét nghiệm liên quan:</strong>
              <mat-list dense>
                @for (lab of linkedLabResults; track lab.testName) {
                <mat-list-item>
                  <mat-icon matListItemIcon>science</mat-icon>
                  <span matListItemTitle>{{ lab.testName }}</span>
                  <span matListItemLine>
                    {{ lab.result || 'Chưa có KQ' }}
                    @if (lab.isAbnormal) {
                    <span class="abnormal-badge">Bất thường</span>
                    }
                    <span class="lab-status-inline" [class]="'lab-status-' + lab.statusCode.toLowerCase()">
                      {{ lab.statusName }}
                    </span>
                  </span>
                </mat-list-item>
                }
              </mat-list>
            </div>
            }
          </mat-card-content>
        </mat-card>

        <!-- A: Assessment -->
        <mat-card class="soap-card soap-a">
          <mat-card-header>
            <mat-card-title><span class="soap-label">A</span> Assessment (Đánh giá)</mat-card-title>
          </mat-card-header>
          <mat-card-content>
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
                    <span class="diag-type">{{ d.isPrimary ? 'Chính' : 'Phụ' }}</span>
                  </span>
                  @if (d.notes) {
                  <span matListItemLine>{{ d.notes }}</span>
                  }
                </mat-list-item>
                }
              </mat-list>
            </div>
            } @else {
            <p class="empty-section">Chưa có chẩn đoán</p>
            }
          </mat-card-content>
        </mat-card>

        <!-- P: Plan -->
        <mat-card class="soap-card soap-p">
          <mat-card-header>
            <mat-card-title><span class="soap-label">P</span> Plan (Kế hoạch)</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            @if (encounter.assessment) {
            <div class="soap-section">
              <strong>Nhận định:</strong>
              <p>{{ encounter.assessment }}</p>
            </div>
            }
            @if (encounter.plan) {
            <div class="soap-section">
              <strong>Kế hoạch điều trị:</strong>
              <p>{{ encounter.plan }}</p>
            </div>
            }
            @if (!encounter.assessment && !encounter.plan) {
            <div class="soap-section">
              <p class="empty-section">Chưa có kế hoạch điều trị</p>
            </div>
            }

            <!-- Linked Prescriptions -->
            @if (linkedPrescriptions.length > 0) {
            <div class="soap-section">
              <strong>Đơn thuốc đã kê:</strong>
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
          </mat-card-content>
        </mat-card>
      </div>

      <!-- Timeline -->
      <mat-card class="timeline-card">
        <mat-card-header><mat-card-title><mat-icon>timeline</mat-icon> Dòng thời gian</mat-card-title></mat-card-header>
        <mat-card-content>
          <mat-list>
            <mat-list-item>
              <mat-icon matListItemIcon>add_circle</mat-icon>
              <div matListItemTitle>Khởi tạo</div>
              <div matListItemLine>{{ encounter.createdAt | date:'medium' }}</div>
            </mat-list-item>
            @if (encounter.updatedAt) {
            <mat-list-item>
              <mat-icon matListItemIcon>update</mat-icon>
              <div matListItemTitle>Cập nhật lần cuối</div>
              <div matListItemLine>{{ encounter.updatedAt | date:'medium' }}</div>
            </mat-list-item>
            }
          </mat-list>
        </mat-card-content>
      </mat-card>
    </div>
    }
  `,
    styles: [`
    .detail { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; flex-wrap: wrap; gap: 12px; }
    .header-sub { color: #666; font-size: 14px; margin-top: 4px; }
    .header-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
    .status-badge { padding: 4px 16px; border-radius: 16px; font-weight: 500; font-size: 14px; white-space: nowrap; }
    .status-in_progress { background: #e3f2fd; color: #1565c0; }
    .status-completed { background: #e8f5e9; color: #2e7d32; }
    .status-signed { background: #f3e5f5; color: #7b1fa2; }
    .status-cancelled { background: #fce4ec; color: #c62828; }
    .overview-card { margin-bottom: 24px; }
    .overview-card mat-card-title { display: flex; align-items: center; gap: 8px; }
    .overview-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    /* SOAP layout */
    .soap-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 24px; }
    .soap-card { border-top: 4px solid #e0e0e0; }
    .soap-s { border-top-color: #42a5f5; }
    .soap-o { border-top-color: #66bb6a; }
    .soap-a { border-top-color: #ffa726; }
    .soap-p { border-top-color: #ab47bc; }
    .soap-card mat-card-title { display: flex; align-items: center; gap: 8px; font-size: 16px; }
    .soap-label { display: inline-flex; align-items: center; justify-content: center; width: 28px; height: 28px; border-radius: 50%; font-weight: 700; font-size: 14px; color: #fff; }
    .soap-s .soap-label { background: #42a5f5; }
    .soap-o .soap-label { background: #66bb6a; }
    .soap-a .soap-label { background: #ffa726; }
    .soap-p .soap-label { background: #ab47bc; }
    .soap-section { margin-bottom: 16px; }
    .soap-section:last-child { margin-bottom: 0; }
    .soap-section p { margin: 6px 0; line-height: 1.5; }
    .empty-section { color: #999; font-style: italic; }
    .hpi-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 6px; margin-top: 8px; font-size: 13px; }
    .vitals-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-top: 8px; }
    .diag-type { font-size: 11px; padding: 1px 8px; border-radius: 8px; background: #f5f5f5; margin-left: 6px; }
    .abnormal-badge { font-size: 10px; padding: 1px 6px; border-radius: 8px; background: #ffebee; color: #c62828; margin-left: 6px; }
    .lab-status-inline { font-size: 10px; padding: 1px 6px; border-radius: 8px; margin-left: 6px; }
    .lab-status-pending, .lab-status-ordered { background: #fff3e0; color: #e65100; }
    .lab-status-collected { background: #e3f2fd; color: #1565c0; }
    .lab-status-completed { background: #e8f5e9; color: #2e7d32; }
    .lab-status-abnormal { background: #ffebee; color: #b71c1c; }
    .timeline-card { margin-bottom: 24px; }
    .timeline-card mat-card-title { display: flex; align-items: center; gap: 8px; }
    @media (max-width: 768px) {
      .soap-grid { grid-template-columns: 1fr; }
      .overview-grid { grid-template-columns: 1fr; }
    }
  `],
})
export class EncounterDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  encounter?: Encounter;
  linkedLabResults: LabResultDisplay[] = [];
  linkedPrescriptions: Prescription[] = [];

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
    const id = this.route.snapshot.paramMap.get('id')!;
    this.clinicalService.getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: e => {
          this.encounter = e;
          this.loadLinkedData();
          this.cdr.markForCheck();
        },
        error: () => {
          this.snackBar.open('Không thể tải thông tin lượt khám', 'Đóng', { duration: 5000 });
        },
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
