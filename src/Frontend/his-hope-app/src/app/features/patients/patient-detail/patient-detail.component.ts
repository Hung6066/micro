import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, forkJoin, takeUntil } from 'rxjs';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { Encounter } from '@core/models/encounter.model';
import { Prescription } from '@core/models/prescription.model';
import { LabOrder } from '@core/models/lab-order.model';

@Component({
  selector: 'app-patient-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="patient-detail" *ngIf="patient">
      <div class="header">
        <div>
          <h1>{{ patient.fullName }}</h1>
          <p class="subtitle">Mã BN: {{ patient.id | slice:0:8 }}... | {{ patient.genderName }} | Tuổi: {{ patient.age }}</p>
        </div>
        <div class="header-actions">
          <button mat-raised-button color="accent" [routerLink]="['/patients', patient.id, 'edit']"
                  *ngIf="patient.isActive">
            <mat-icon>edit</mat-icon> Sửa
          </button>
          <button mat-stroked-button color="primary" [routerLink]="['/appointments/new']"
                  [queryParams]="{patientId: patient.id}">
            <mat-icon>calendar_today</mat-icon> Đặt lịch hẹn
          </button>
          <button mat-raised-button [color]="patient.isActive ? 'warn' : 'primary'"
                  (click)="toggleActive()">
            <mat-icon>{{ patient.isActive ? 'block' : 'check_circle' }}</mat-icon>
            {{ patient.isActive ? 'Vô hiệu hóa' : 'Kích hoạt lại' }}
          </button>
        </div>
      </div>

      <mat-tab-group dynamicHeight>
        <!-- Tab 1: Thông tin -->
        <mat-tab label="Thông tin">
          <div class="tab-content">
            <div class="detail-grid">
              <mat-card>
                <mat-card-header><mat-card-title>Thông tin cá nhân</mat-card-title></mat-card-header>
                <mat-card-content>
                  <p><strong>Ngày sinh:</strong> {{ patient.dateOfBirth | date:'mediumDate' }}</p>
                  <p><strong>Giới tính:</strong> {{ patient.genderName }}</p>
                  <p><strong>Nhóm máu:</strong> {{ patient.bloodTypeName || '-' }}</p>
                  <p><strong>CMND/CCCD:</strong> {{ patient.nationalId || '-' }}</p>
                  <p><strong>Nghề nghiệp:</strong> {{ patient.occupation || '-' }}</p>
                </mat-card-content>
              </mat-card>

              <mat-card>
                <mat-card-header><mat-card-title>Liên hệ</mat-card-title></mat-card-header>
                <mat-card-content>
                  <p><strong>Điện thoại:</strong> {{ patient.phone }}</p>
                  <p><strong>Email:</strong> {{ patient.email || '-' }}</p>
                  <p><strong>Địa chỉ:</strong> {{ patient.street }}, {{ patient.district }}, {{ patient.city }}</p>
                  <p><strong>Bảo hiểm:</strong> {{ patient.insuranceId || '-' }}</p>
                  <p><strong>Liên hệ khẩn cấp:</strong> {{ patient.emergencyContactName || '-' }} - {{ patient.emergencyContactPhone || '-' }}</p>
                </mat-card-content>
              </mat-card>
            </div>

            <mat-card class="conditions-card">
              <mat-card-header>
                <mat-card-title>Bệnh lý ({{ patient.conditions.length }})</mat-card-title>
              </mat-card-header>
              <mat-card-content>
                <mat-list *ngIf="patient.conditions.length > 0; else noConditions">
                  <mat-list-item *ngFor="let c of patient.conditions">
                    <mat-icon matListItemIcon>info</mat-icon>
                    <span matListItemTitle>{{ c.conditionName }} <small *ngIf="c.icd10Code">({{ c.icd10Code }})</small></span>
                    <span matListItemLine>{{ c.isChronic ? 'Mạn tính' : 'Cấp tính' }} | {{ c.isActive ? 'Đang hoạt động' : 'Đã khỏi' }}</span>
                  </mat-list-item>
                </mat-list>
                <ng-template #noConditions><p class="empty">Không có bệnh lý nào</p></ng-template>
              </mat-card-content>
            </mat-card>

            <mat-card>
              <mat-card-header>
                <mat-card-title>Dị ứng ({{ patient.allergies.length }})</mat-card-title>
              </mat-card-header>
              <mat-card-content>
                <mat-list *ngIf="patient.allergies.length > 0; else noAllergies">
                  <mat-list-item *ngFor="let a of patient.allergies">
                    <mat-icon matListItemIcon>warning</mat-icon>
                    <span matListItemTitle>{{ a.allergen }}</span>
                    <span matListItemLine>{{ a.reaction || 'Không rõ phản ứng' }} | {{ a.severity || 'N/A' }}</span>
                  </mat-list-item>
                </mat-list>
                <ng-template #noAllergies><p class="empty">Không có dị ứng nào</p></ng-template>
              </mat-card-content>
            </mat-card>
          </div>
        </mat-tab>

        <!-- Tab 2: Lịch sử khám -->
        <mat-tab label="Lịch sử khám ({{ encounters.length }})">
          <div class="tab-content">
            <div *ngIf="loadingEncounters" class="tab-loading"><mat-spinner diameter="32"></mat-spinner></div>
            <div *ngIf="!loadingEncounters && encounters.length === 0" class="tab-empty">
              <mat-icon>inbox</mat-icon>
              <p>Bệnh nhân chưa có lượt khám nào</p>
            </div>
            <table mat-table [dataSource]="encounters" *ngIf="!loadingEncounters && encounters.length > 0" class="records-table">
              <ng-container matColumnDef="encounterDate">
                <th mat-header-cell *matHeaderCellDef>Ngày khám</th>
                <td mat-cell *matCellDef="let e">{{ e.encounterDate | date:'dd/MM/yyyy HH:mm' }}</td>
              </ng-container>
              <ng-container matColumnDef="encounterType">
                <th mat-header-cell *matHeaderCellDef>Loại</th>
                <td mat-cell *matCellDef="let e">{{ e.encounterTypeName || e.encounterType }}</td>
              </ng-container>
              <ng-container matColumnDef="chiefComplaint">
                <th mat-header-cell *matHeaderCellDef>Lý do khám</th>
                <td mat-cell *matCellDef="let e">{{ e.chiefComplaint || '-' }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
                <td mat-cell *matCellDef="let e">
                  <span class="status-badge" [class]="'status-' + e.status.toLowerCase()">
                    {{ e.statusName || e.status }}
                  </span>
                </td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let e">
                  <button mat-icon-button [routerLink]="['/clinical', e.id]" matTooltip="Xem chi tiết">
                    <mat-icon>visibility</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['encounterDate','encounterType','chiefComplaint','status','actions']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['encounterDate','encounterType','chiefComplaint','status','actions'];"></tr>
            </table>
          </div>
        </mat-tab>

        <!-- Tab 3: Đơn thuốc -->
        <mat-tab label="Đơn thuốc ({{ prescriptions.length }})">
          <div class="tab-content">
            <div *ngIf="loadingPrescriptions" class="tab-loading"><mat-spinner diameter="32"></mat-spinner></div>
            <div *ngIf="!loadingPrescriptions && prescriptions.length === 0" class="tab-empty">
              <mat-icon>medication</mat-icon>
              <p>Bệnh nhân chưa có đơn thuốc nào</p>
            </div>
            <table mat-table [dataSource]="prescriptions" *ngIf="!loadingPrescriptions && prescriptions.length > 0" class="records-table">
              <ng-container matColumnDef="prescribedDate">
                <th mat-header-cell *matHeaderCellDef>Ngày kê</th>
                <td mat-cell *matCellDef="let p">{{ p.prescribedDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="medicationName">
                <th mat-header-cell *matHeaderCellDef>Thuốc</th>
                <td mat-cell *matCellDef="let p">{{ p.medicationName }}</td>
              </ng-container>
              <ng-container matColumnDef="dosage">
                <th mat-header-cell *matHeaderCellDef>Liều dùng</th>
                <td mat-cell *matCellDef="let p">{{ p.dosage }}</td>
              </ng-container>
              <ng-container matColumnDef="frequency">
                <th mat-header-cell *matHeaderCellDef>Tần suất</th>
                <td mat-cell *matCellDef="let p">{{ p.frequency }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
                <td mat-cell *matCellDef="let p">
                  <span class="status-badge" [class]="'rx-status-' + p.status.toLowerCase()">
                    {{ p.statusName || p.status }}
                  </span>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['prescribedDate','medicationName','dosage','frequency','status']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['prescribedDate','medicationName','dosage','frequency','status'];"></tr>
            </table>
          </div>
        </mat-tab>

        <!-- Tab 4: Xét nghiệm -->
        <mat-tab label="Xét nghiệm ({{ labOrders.length }})">
          <div class="tab-content">
            <div *ngIf="loadingLabs" class="tab-loading"><mat-spinner diameter="32"></mat-spinner></div>
            <div *ngIf="!loadingLabs && labOrders.length === 0" class="tab-empty">
              <mat-icon>science</mat-icon>
              <p>Bệnh nhân chưa có xét nghiệm nào</p>
            </div>
            <table mat-table [dataSource]="labOrders" *ngIf="!loadingLabs && labOrders.length > 0" class="records-table">
              <ng-container matColumnDef="orderDate">
                <th mat-header-cell *matHeaderCellDef>Ngày chỉ định</th>
                <td mat-cell *matCellDef="let l">{{ l.orderDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="testName">
                <th mat-header-cell *matHeaderCellDef>Xét nghiệm</th>
                <td mat-cell *matCellDef="let l">{{ l.testName }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
                <td mat-cell *matCellDef="let l">
                  <span class="status-badge" [class]="'lab-status-' + l.status.toLowerCase()">
                    {{ l.statusName || l.status }}
                  </span>
                </td>
              </ng-container>
              <ng-container matColumnDef="result">
                <th mat-header-cell *matHeaderCellDef>Kết quả</th>
                <td mat-cell *matCellDef="let l">{{ l.result || '-' }}</td>
              </ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let l">
                  <button mat-icon-button [routerLink]="['/labs', l.id]" matTooltip="Xem chi tiết">
                    <mat-icon>visibility</mat-icon>
                  </button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['orderDate','testName','status','result','actions']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['orderDate','testName','status','result','actions'];"></tr>
            </table>
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: [`
    .patient-detail {
      max-width: var(--max-width-container, 1200px);
      margin: 0 auto;
      padding: 32px 24px;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 24px;
      flex-wrap: wrap;
      gap: 12px;
    }

    .header h1 {
      font-size: 24px;
      font-weight: 600;
      letter-spacing: -0.01em;
    }

    .header-actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }

    .subtitle {
      color: var(--text-secondary, #787774);
      margin-top: 4px;
    }

    .tab-content {
      padding: 20px 0;
    }

    .tab-loading {
      display: flex;
      justify-content: center;
      padding: 48px;
    }

    .tab-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 48px;
      color: var(--text-muted, #A1A09B);
    }

    .tab-empty mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
    }

    .detail-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
      margin-bottom: 20px;
    }

    .conditions-card {
      margin-bottom: 20px;
    }

    .empty {
      color: var(--text-muted, #A1A09B);
      font-style: italic;
      padding: 12px;
    }

    mat-card-content p {
      margin: 8px 0;
      font-size: 14px;
    }

    .records-table {
      width: 100%;
    }

    ::ng-deep .mat-mdc-tab-body-content { overflow: hidden; }

    @media (max-width: 768px) {
      .detail-grid {
        grid-template-columns: 1fr;
      }
    }
  `],
})
export class PatientDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  patient?: Patient;

  encounters: Encounter[] = [];
  prescriptions: Prescription[] = [];
  labOrders: LabOrder[] = [];

  loadingEncounters = false;
  loadingPrescriptions = false;
  loadingLabs = false;

  constructor(
    private route: ActivatedRoute,
    private patientService: PatientService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.params['id'];
    this.patientService.getById(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (p) => {
          this.patient = p;
          this.loadTabData();
          this.cdr.markForCheck();
        },
        error: () => {
          this.snackBar.open('Không thể tải thông tin bệnh nhân', 'Đóng', { duration: 5000 });
        },
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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
