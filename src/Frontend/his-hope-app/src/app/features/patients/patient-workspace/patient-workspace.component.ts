import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
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
        StartEncounterDialogComponent,
        OrderLabDialogComponent,
        PrescribeDialogComponent,
        ScheduleDialogComponent,
        RecordPaymentDialogComponent,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <!-- Sticky Patient Header Bar -->
    @if (patient) {
    <div class="patient-header">
      <div class="patient-info">
        <div class="patient-avatar">{{ patient.fullName.charAt(0) }}</div>
        <div class="patient-details">
          <h1>{{ patient.fullName }}</h1>
          <div class="patient-meta">
            <span class="meta-item"><mat-icon>badge</mat-icon> Mã BN: {{ patient.id }}</span>
            <span class="meta-item"><mat-icon>cake</mat-icon> {{ patient.age }} tuổi ({{ patient.dateOfBirth | date:'dd/MM/yyyy' }})</span>
            <span class="meta-item"><mat-icon>wc</mat-icon> {{ patient.genderName }}</span>
            @if (patient.bloodTypeName) {
            <span class="meta-item">
              <mat-icon>bloodtype</mat-icon> {{ patient.bloodTypeName }}
            </span>
            }
          </div>
        </div>
      </div>

      @if (patient.allergies.length > 0) {
      <div class="patient-alerts">
        <mat-chip-set>
          @for (a of patient.allergies; track a) {
          <mat-chip class="allergy-chip"
                    [matTooltip]="'Phản ứng: ' + (a.reaction || 'Không rõ') + ' | Mức độ: ' + (a.severity || 'N/A')">
            <mat-icon matChipAvatar>warning</mat-icon>
            {{ a.allergen }}
          </mat-chip>
          }
        </mat-chip-set>
      </div>
      }

      @if (activeConditions.length > 0) {
      <div class="patient-conditions">
        <mat-chip-set>
          @for (c of activeConditions; track c) {
          <mat-chip class="condition-chip"
                    [matTooltip]="(c.isChronic ? 'Mạn tính' : 'Cấp tính') + ' | ICD-10: ' + (c.icd10Code || 'N/A')">
            <mat-icon matChipAvatar>info</mat-icon>
            {{ c.conditionName }}
          </mat-chip>
          }
        </mat-chip-set>
      </div>
      }

      <div class="quick-actions">
        <button mat-raised-button color="primary" (click)="startNewEncounter()">
          <mat-icon>add_circle</mat-icon> Khám mới
        </button>
        <button mat-raised-button color="accent" (click)="orderLab()">
          <mat-icon>science</mat-icon> Xét nghiệm
        </button>
        <button mat-raised-button color="primary" (click)="prescribe()">
          <mat-icon>medication</mat-icon> Kê đơn
        </button>
        <button mat-stroked-button (click)="scheduleAppointment()">
          <mat-icon>calendar_today</mat-icon> Lịch hẹn
        </button>
      </div>
    </div>
    }

    @if (!patient && !error) {
    <div class="loading-header">
      <mat-spinner diameter="32"></mat-spinner>
      <span>Đang tải thông tin bệnh nhân...</span>
    </div>
    }

    @if (error) {
    <div class="error-box">
      <mat-icon>error_outline</mat-icon>
      <span>{{ error }}</span>
      <button mat-stroked-button (click)="loadPatient()">Thử lại</button>
    </div>
    }

    <!-- Tab Group -->
    @if (patient) {
    <div class="workspace-tabs">
      <mat-tab-group dynamicHeight>
        <!-- Tab 1: Encounters -->
        <mat-tab label="Lịch sử khám ({{ encounters.length }})">
          <div class="tab-body">
            <div class="tab-toolbar">
              <h3>Lịch sử khám bệnh</h3>
              <button mat-stroked-button color="primary" (click)="startNewEncounter()">
                <mat-icon>add</mat-icon> Bắt đầu khám
              </button>
            </div>
            @if (loadingEncounters) {
            <div class="tab-loading"><mat-spinner diameter="28"></mat-spinner></div>
            }
            @if (!loadingEncounters && encounters.length === 0) {
            <div class="tab-empty">
              <mat-icon>inbox</mat-icon>
              <p>Bệnh nhân chưa có lượt khám nào</p>
            </div>
            }
            @if (!loadingEncounters && encounters.length > 0) {
            <table mat-table [dataSource]="encounters" class="workspace-table">
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
                  <span class="status-badge" [class]="'status-' + e.status.toLowerCase()">{{ e.statusName || e.status }}</span>
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
              <tr mat-header-row *matHeaderRowDef="encounterColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: encounterColumns;" class="clickable-row"
                  (click)="toggleEncounterDetail(row)"></tr>
              <tr mat-row *matRowDef="let row; columns: ['expanded'];" class="expanded-row"
                  [style.display]="expandedEncounterId === row.id ? '' : 'none'">
                <td [attr.colspan]="encounterColumns.length" class="soap-preview">
                  <div class="soap-content">
                    @if (row.hpi) {
                    <div class="soap-section">
                      <h4>📋 Bệnh sử (HPI)</h4>
                      @if (row.hpi.onset) {<p><strong>Khởi phát:</strong> {{ row.hpi.onset }}</p>}
                      @if (row.hpi.characteristics) {<p><strong>Tính chất:</strong> {{ row.hpi.characteristics }}</p>}
                      @if (row.hpi.duration) {<p><strong>Thời gian:</strong> {{ row.hpi.duration }}</p>}
                    </div>
                    }
                    @if (row.vitalSigns) {
                    <div class="soap-section">
                      <h4>💓 Dấu hiệu sinh tồn</h4>
                      <div class="vitals-row">
                        @if (row.vitalSigns.temperature) {<span>🌡 {{ row.vitalSigns.temperature }}°C</span>}
                        @if (row.vitalSigns.heartRate) {<span>💓 {{ row.vitalSigns.heartRate }} l/ph</span>}
                        @if (row.vitalSigns.systolicBP) {<span>🩸 {{ row.vitalSigns.systolicBP }}/{{ row.vitalSigns.diastolicBP }}</span>}
                        @if (row.vitalSigns.oxygenSaturation) {<span>🫁 SpO2: {{ row.vitalSigns.oxygenSaturation }}%</span>}
                      </div>
                    </div>
                    }
                    @if (row.assessment) {
                    <div class="soap-section">
                      <h4>📝 Đánh giá</h4>
                      <p>{{ row.assessment }}</p>
                    </div>
                    }
                    @if (row.plan) {
                    <div class="soap-section">
                      <h4>📋 Kế hoạch</h4>
                      <pre>{{ row.plan }}</pre>
                    </div>
                    }
                  </div>
                </td>
              </tr>
            </table>
            }
          </div>
        </mat-tab>

        <!-- Tab 2: Appointments -->
        <mat-tab label="Lịch hẹn ({{ appointments.length }})">
          <div class="tab-body">
            <div class="tab-toolbar">
              <h3>Lịch hẹn</h3>
              <button mat-stroked-button color="primary" (click)="scheduleAppointment()">
                <mat-icon>add</mat-icon> Đặt lịch
              </button>
            </div>
            @if (loadingAppointments) {
            <div class="tab-loading"><mat-spinner diameter="28"></mat-spinner></div>
            }
            @if (!loadingAppointments && appointments.length === 0) {
            <div class="tab-empty">
              <mat-icon>event_busy</mat-icon>
              <p>Bệnh nhân chưa có lịch hẹn</p>
            </div>
            }
            @if (!loadingAppointments && appointments.length > 0) {
            <table mat-table [dataSource]="appointments" class="workspace-table">
              <ng-container matColumnDef="scheduledDate">
                <th mat-header-cell *matHeaderCellDef>Ngày</th>
                <td mat-cell *matCellDef="let a">{{ a.scheduledDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="startTime">
                <th mat-header-cell *matHeaderCellDef>Giờ</th>
                <td mat-cell *matCellDef="let a">{{ a.startTime }} - {{ a.endTime }}</td>
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
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let a">
                  @if (a.status === 'scheduled') {
                  <button mat-icon-button (click)="checkInAppointment(a.id)" matTooltip="Check-in">
                    <mat-icon>login</mat-icon>
                  </button>
                  }
                  @if (a.status === 'checked_in') {
                  <button mat-icon-button (click)="checkOutAppointment(a.id)" matTooltip="Check-out">
                    <mat-icon>logout</mat-icon>
                  </button>
                  }
                  @if (a.status === 'scheduled' || a.status === 'checked_in') {
                  <button mat-icon-button (click)="cancelAppointment(a.id)" matTooltip="Hủy">
                    <mat-icon>cancel</mat-icon>
                  </button>
                  }
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="appointmentColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: appointmentColumns;"></tr>
            </table>
            }
          </div>
        </mat-tab>

        <!-- Tab 3: Lab Orders -->
        <mat-tab label="Xét nghiệm ({{ labOrders.length }})">
          <div class="tab-body">
            <div class="tab-toolbar">
              <h3>Xét nghiệm</h3>
              <button mat-stroked-button color="primary" (click)="orderLab()">
                <mat-icon>add</mat-icon> Chỉ định mới
              </button>
            </div>
            @if (loadingLabs) {
            <div class="tab-loading"><mat-spinner diameter="28"></mat-spinner></div>
            }
            @if (!loadingLabs && labOrders.length === 0) {
            <div class="tab-empty">
              <mat-icon>science</mat-icon>
              <p>Bệnh nhân chưa có xét nghiệm nào</p>
            </div>
            }
            @if (!loadingLabs && labOrders.length > 0) {
            <table mat-table [dataSource]="labOrders" class="workspace-table">
              <ng-container matColumnDef="orderDate">
                <th mat-header-cell *matHeaderCellDef>Ngày chỉ định</th>
                <td mat-cell *matCellDef="let l">{{ l.orderDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="testName">
                <th mat-header-cell *matHeaderCellDef>Xét nghiệm</th>
                <td mat-cell *matCellDef="let l">
                  @for (t of l.tests; track t) {
                  <div>{{ t.testName }}</div>
                  }
                </td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
                <td mat-cell *matCellDef="let l">
                  <span class="status-badge" [class]="'lab-status-' + l.statusCode.toLowerCase()">{{ l.statusName || l.statusCode }}</span>
                </td>
              </ng-container>
              <ng-container matColumnDef="result">
                <th mat-header-cell *matHeaderCellDef>Kết quả</th>
                <td mat-cell *matCellDef="let l">
                  @if (hasAbnormalFlag(l)) {
                  <span class="abnormal-flag">
                    <mat-icon>error</mat-icon> Bất thường
                  </span>
                  }
                  @if (!hasAbnormalFlag(l) && isCompleted(l)) {
                  <span>Bình thường</span>
                  }
                  @if (!isCompleted(l)) {
                  <span>-</span>
                  }
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="labColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: labColumns;"></tr>
            </table>
            }
          </div>
        </mat-tab>

        <!-- Tab 4: Prescriptions -->
        <mat-tab label="Đơn thuốc ({{ prescriptions.length }})">
          <div class="tab-body">
            <div class="tab-toolbar">
              <h3>Đơn thuốc</h3>
              <button mat-stroked-button color="primary" (click)="prescribe()">
                <mat-icon>add</mat-icon> Kê đơn
              </button>
            </div>
            @if (loadingPrescriptions) {
            <div class="tab-loading"><mat-spinner diameter="28"></mat-spinner></div>
            }
            @if (!loadingPrescriptions && prescriptions.length === 0) {
            <div class="tab-empty">
              <mat-icon>medication</mat-icon>
              <p>Bệnh nhân chưa có đơn thuốc nào</p>
            </div>
            }
            @if (!loadingPrescriptions && prescriptions.length > 0) {
            <table mat-table [dataSource]="prescriptions" class="workspace-table">
              <ng-container matColumnDef="prescribedAt">
                <th mat-header-cell *matHeaderCellDef>Ngày kê</th>
                <td mat-cell *matCellDef="let p">{{ p.prescribedAt | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="medicationName">
                <th mat-header-cell *matHeaderCellDef>Thuốc</th>
                <td mat-cell *matCellDef="let p">{{ p.medicationName }} ({{ p.strength }})</td>
              </ng-container>
              <ng-container matColumnDef="dosageInstructions">
                <th mat-header-cell *matHeaderCellDef>Liều dùng</th>
                <td mat-cell *matCellDef="let p">{{ p.dosageInstructions }}</td>
              </ng-container>
              <ng-container matColumnDef="quantity">
                <th mat-header-cell *matHeaderCellDef>SL</th>
                <td mat-cell *matCellDef="let p">{{ p.quantity }}</td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
                <td mat-cell *matCellDef="let p">
                  <span class="status-badge" [class]="'rx-status-' + p.statusCode.toLowerCase()">{{ p.statusName || p.statusCode }}</span>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="prescriptionColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: prescriptionColumns;"></tr>
            </table>
            }
          </div>
        </mat-tab>

        <!-- Tab 5: Billing -->
        <mat-tab label="Thanh toán ({{ invoices.length }})">
          <div class="tab-body">
            <div class="tab-toolbar">
              <h3>Thanh toán</h3>
              <button mat-stroked-button color="primary" (click)="recordPayment()">
                <mat-icon>payments</mat-icon> Ghi nhận thanh toán
              </button>
            </div>
            @if (loadingInvoices) {
            <div class="tab-loading"><mat-spinner diameter="28"></mat-spinner></div>
            }
            @if (!loadingInvoices && invoices.length === 0) {
            <div class="tab-empty">
              <mat-icon>receipt</mat-icon>
              <p>Bệnh nhân chưa có hóa đơn</p>
            </div>
            }
            @if (!loadingInvoices && invoices.length > 0) {
            <table mat-table [dataSource]="invoices" class="workspace-table">
              <ng-container matColumnDef="invoiceNumber">
                <th mat-header-cell *matHeaderCellDef>Số HĐ</th>
                <td mat-cell *matCellDef="let i">{{ i.invoiceNumber }}</td>
              </ng-container>
              <ng-container matColumnDef="invoiceDate">
                <th mat-header-cell *matHeaderCellDef>Ngày</th>
                <td mat-cell *matCellDef="let i">{{ i.invoiceDate | date:'dd/MM/yyyy' }}</td>
              </ng-container>
              <ng-container matColumnDef="totalAmount">
                <th mat-header-cell *matHeaderCellDef>Tổng tiền</th>
                <td mat-cell *matCellDef="let i">{{ i.totalAmount | number }}₫</td>
              </ng-container>
              <ng-container matColumnDef="balanceDue">
                <th mat-header-cell *matHeaderCellDef>Còn nợ</th>
                <td mat-cell *matCellDef="let i" [class.text-danger]="i.balanceDue > 0">
                  {{ i.balanceDue | number }}₫
                </td>
              </ng-container>
              <ng-container matColumnDef="status">
                <th mat-header-cell *matHeaderCellDef>Trạng thái</th>
                <td mat-cell *matCellDef="let i">
                  <span class="status-badge" [class]="'inv-status-' + i.statusCode.toLowerCase()">{{ i.statusName || i.statusCode }}</span>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="invoiceColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: invoiceColumns;"></tr>
            </table>
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
    }
  `,
    styles: [`
    :host { display: block; height: 100%; }

    /* ── Patient Header ── */
    .patient-header {
      background: var(--surface-white, #FFFFFF);
      border-bottom: 1px solid var(--border-default, #EAEAEA);
      padding: 16px 24px;
      position: sticky;
      top: 0;
      z-index: 10;
      box-shadow: none;
    }

    .patient-info {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-bottom: 8px;
    }

    .patient-avatar {
      width: 48px;
      height: 48px;
      border-radius: 8px;
      background: var(--pastel-blue, #E1F3FE);
      color: var(--pastel-blue-text, #1A6BB5);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 20px;
      font-weight: 600;
      flex-shrink: 0;
    }

    .patient-details h1 {
      margin: 0;
      font-size: 22px;
      font-weight: 600;
      letter-spacing: -0.01em;
    }

    .patient-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      margin-top: 4px;
      color: var(--text-secondary, #787774);
      font-size: 13px;
    }

    .meta-item {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .meta-item mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      opacity: 0.6;
    }

    .patient-alerts { margin: 8px 0; }

    .allergy-chip {
      background: var(--pastel-red, #FDEBEC) !important;
      color: var(--pastel-red-text, #C25450) !important;
    }

    .condition-chip {
      background: var(--pastel-blue, #E1F3FE) !important;
      color: var(--pastel-blue-text, #1A6BB5) !important;
    }

    .quick-actions {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-top: 8px;
      padding-top: 8px;
      border-top: 1px solid var(--border-light, #F0F0EE);
    }

    .loading-header {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 48px;
      color: var(--text-secondary, #787774);
    }

    .error-box {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 16px;
      background: var(--pastel-red, #FDEBEC);
      color: var(--pastel-red-text, #C25450);
      border-radius: var(--radius-card, 8px);
      margin: 24px;
    }

    /* ── Tabs ── */
    .workspace-tabs {
      padding: 0 24px 24px;
    }

    .tab-body {
      padding: 16px 0;
      min-height: 200px;
    }

    .tab-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }

    .tab-toolbar h3 {
      margin: 0;
      font-weight: 600;
      font-size: 16px;
      color: var(--text-primary, #1A1A1A);
    }

    .tab-loading {
      display: flex;
      justify-content: center;
      padding: 32px;
    }

    .tab-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 48px;
      color: var(--text-muted, #A1A09B);
    }

    .tab-empty mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      opacity: 0.4;
    }

    /* ── Tables ── */
    .workspace-table { width: 100%; }

    .clickable-row {
      cursor: pointer;
      transition: background-color 150ms ease;
    }

    .clickable-row:hover {
      background: rgba(0, 0, 0, 0.02);
    }

    .expanded-row td { padding: 0 !important; }

    .soap-preview {
      background: var(--bg-warm-alt, #FBFBFA);
      border-bottom: 1px solid var(--border-light, #F0F0EE);
    }

    .soap-content { padding: 16px 24px; }
    .soap-section { margin-bottom: 12px; }

    .soap-section h4 {
      margin: 0 0 4px;
      font-size: 13px;
      font-weight: 600;
      color: var(--text-secondary, #787774);
    }

    .soap-section p {
      margin: 2px 0;
      font-size: 13px;
      color: var(--text-primary, #1A1A1A);
    }

    .soap-section pre {
      margin: 4px 0;
      font-size: 13px;
      white-space: pre-wrap;
      color: var(--text-primary, #1A1A1A);
      background: var(--surface-white, #FFFFFF);
      padding: 8px;
      border-radius: var(--radius-input, 4px);
      border: 1px solid var(--border-light, #F0F0EE);
    }

    .vitals-row {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
    }

    .vitals-row span {
      background: var(--surface-white, #FFFFFF);
      padding: 4px 12px;
      border-radius: var(--radius-badge, 4px);
      font-size: 13px;
      border: 1px solid var(--border-light, #F0F0EE);
    }

    /* ── Status Badges (minimal overrides; bulk handled in global styles) ── */
    .abnormal-flag {
      display: flex;
      align-items: center;
      gap: 4px;
      color: var(--pastel-red-text, #C25450);
      font-weight: 600;
      font-size: 12px;
    }

    .abnormal-flag mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    .text-danger {
      color: var(--pastel-red-text, #C25450);
      font-weight: 600;
    }

    ::ng-deep .mat-mdc-tab-body-content { overflow: hidden; }
  `]
})
export class PatientWorkspaceComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

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
          this.error = 'Không thể tải thông tin bệnh nhân';
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
          this.snackBar.open('Đã check-in lịch hẹn', 'Đóng', { duration: 3000 });
          this.loadAppointments();
        },
        error: () => this.snackBar.open('Không thể check-in', 'Đóng', { duration: 5000 }),
      });
  }

  checkOutAppointment(id: string): void {
    this.appointmentService.checkOut(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã check-out lịch hẹn', 'Đóng', { duration: 3000 });
          this.loadAppointments();
        },
        error: () => this.snackBar.open('Không thể check-out', 'Đóng', { duration: 5000 }),
      });
  }

  cancelAppointment(id: string): void {
    this.appointmentService.cancel(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã hủy lịch hẹn', 'Đóng', { duration: 3000 });
          this.loadAppointments();
        },
        error: () => this.snackBar.open('Không thể hủy lịch hẹn', 'Đóng', { duration: 5000 }),
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
