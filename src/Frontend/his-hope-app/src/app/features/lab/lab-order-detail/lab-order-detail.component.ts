import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, takeUntil } from 'rxjs';
import { LabService } from '@core/services/lab.service';
import { LabOrder, LabTest, AbnormalFlag } from '@core/models/lab-order.model';
import { CriticalAlert } from '@core/models/critical-alert.model';
import { LabCriticalAlertService } from '@core/services/lab-critical-alert.service';
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
    selector: 'app-lab-order-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatIconModule, MatButtonModule, MatTableModule,
        MatFormFieldModule, MatInputModule, MatSelectModule, MatProgressSpinnerModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent, HisHopePageSectionComponent,
        HisHopeStateComponent, HisHopeStatusBadgeComponent, HisHopeMetaItemComponent,
        HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'labOrders.detail' | hhTranslate:'Phiếu xét nghiệm'"
                      [subtitle]="labOrder ? ('labOrders.detailSubtitle' | hhTranslate:'Mã phiếu: {{id}}':{id: (labOrder.id | slice:0:8)}) : ''">
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon> {{ 'common.back' | hhTranslate:'Quay lại' }}
        </button>
        @if (labOrder) {
        <hh-status-badge [status]="labOrder.statusCode" [label]="labOrder.statusName" />
        <span class="priority-badge" [class.priority-high]="labOrder.priorityCode === 'high'"
              [class.priority-urgent]="labOrder.priorityCode === 'urgent'"
              [class.priority-routine]="labOrder.priorityCode === 'routine'" style="margin-left: 8px;">
          {{ labOrder.priorityName }}
        </span>
        @if (labOrder.statusCode === 'ordered') {
        <button mat-raised-button color="primary"
                (click)="submitLabOrder()"
                [attr.aria-label]="'labOrders.submitLabel' | hhTranslate:'Gửi phiếu xét nghiệm'">
          <mat-icon>send</mat-icon> {{ 'labOrders.submit' | hhTranslate:'Gửi phiếu' }}
        </button>
        }
        @if (labOrder.statusCode === 'ordered' || labOrder.statusCode === 'in_progress') {
        <button mat-raised-button color="accent"
                (click)="collectSpecimen()"
                [attr.aria-label]="'labOrders.collectLabel' | hhTranslate:'Lấy mẫu bệnh phẩm'">
          <mat-icon>science</mat-icon> {{ 'labOrders.collect' | hhTranslate:'Lấy mẫu' }}
        </button>
        }
        @if (labOrder.statusCode !== 'completed' && labOrder.statusCode !== 'cancelled') {
        <button mat-stroked-button color="warn"
                (click)="cancelLabOrder()"
                [attr.aria-label]="'labOrders.cancelLabel' | hhTranslate:'Hủy phiếu xét nghiệm'">
          <mat-icon>cancel</mat-icon> {{ 'labOrders.cancel' | hhTranslate:'Hủy phiếu' }}
        </button>
        }
        }
      </hh-page-header>

      @if (loading) {
      <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (loadError) {
      <hh-state kind="error" [message]="errorMessage">
        <button mat-stroked-button (click)="loadLabOrder()">{{ 'common.retry' | hhTranslate }}</button>
      </hh-state>
      } @else if (labOrder) {
      @if (criticalAlerts.length > 0) {
      <hh-page-section [title]="'labOrders.section.criticalAlerts' | hhTranslate:'Cảnh báo nghiêm trọng'">
        @for (alert of criticalAlerts; track alert.id) {
        <div class="critical-alert-row">
          <div>
            <p class="critical-alert-title">{{ alert.message }}</p>
            <p class="critical-alert-meta">{{ alert.resultValue }} {{ alert.resultUnit }} • {{ alert.status }}</p>
          </div>
          <div class="critical-alert-actions">
            <button mat-stroked-button color="primary" (click)="acknowledgeCriticalAlert(alert)" [disabled]="alert.status !== 'OPEN'">
              {{ 'labOrders.acknowledge' | hhTranslate:'Ghi nhận' }}
            </button>
          </div>
        </div>
        }
      </hh-page-section>
      }

      <hh-page-section [title]="'labOrders.section.info' | hhTranslate:'Thông tin phiếu'">
        <div class="meta-grid">
          <hh-meta-item [label]="'labOrders.field.patient' | hhTranslate:'Bệnh nhân'" [value]="labOrder.patientName || labOrder.patientId" icon="person" />
          <hh-meta-item [label]="'labOrders.field.provider' | hhTranslate:'Bác sĩ'" [value]="labOrder.providerName || labOrder.providerId" icon="medical_services" />
          @if (labOrder.encounterId) {
          <hh-meta-item [label]="'labOrders.field.encounter' | hhTranslate:'Mã hồ sơ'" [value]="labOrder.encounterId | slice:0:8" icon="folder" />
          }
          <hh-meta-item [label]="'labOrders.field.orderDate' | hhTranslate:'Ngày chỉ định'" [value]="(labOrder.orderDate | date:'medium') || '-'" icon="event" />
          @if (labOrder.notes) {
          <hh-meta-item [label]="'labOrders.field.notes' | hhTranslate:'Ghi chú'" [value]="labOrder.notes" icon="notes" />
          }
        </div>
      </hh-page-section>

      <hh-page-section [title]="'labOrders.section.tests' | hhTranslate:'Danh sách xét nghiệm ({{count}})':{count: labOrder.tests.length}">
        @if (labOrder.tests.length > 0) {
        <mat-table [dataSource]="labOrder.tests">
          <ng-container matColumnDef="testName">
            <mat-header-cell *matHeaderCellDef>{{ 'labOrders.column.test' | hhTranslate:'Xét nghiệm' }}</mat-header-cell>
            <mat-cell *matCellDef="let t">{{ t.testName }}</mat-cell>
          </ng-container>

          <ng-container matColumnDef="specimenType">
            <mat-header-cell *matHeaderCellDef>{{ 'labOrders.column.specimen' | hhTranslate:'Loại mẫu' }}</mat-header-cell>
            <mat-cell *matCellDef="let t">{{ t.specimenType }}</mat-cell>
          </ng-container>

          <ng-container matColumnDef="statusName">
            <mat-header-cell *matHeaderCellDef>{{ 'common.status' | hhTranslate:'Trạng thái' }}</mat-header-cell>
            <mat-cell *matCellDef="let t">
              <hh-status-badge [status]="t.statusCode" [label]="t.statusName" />
            </mat-cell>
          </ng-container>

          <ng-container matColumnDef="result">
            <mat-header-cell *matHeaderCellDef>{{ 'labOrders.column.result' | hhTranslate:'Kết quả' }}</mat-header-cell>
            <mat-cell *matCellDef="let t">
              @if (t.result) {
              <span>
                <span [class.abnormal]="t.result.abnormalFlagCode !== 'none'">
                  {{ t.result.value }} {{ t.result.unit }}
                </span>
                @if (t.result.abnormalFlagCode !== 'none') {
                <span class="abnormal-flag">
                  ({{ t.result.abnormalFlagName }})
                </span>
                }
              </span>
              } @else {
              <span>-</span>
              }
            </mat-cell>
          </ng-container>

          <ng-container matColumnDef="actions">
            <mat-header-cell *matHeaderCellDef>{{ 'labOrders.column.actions' | hhTranslate:'Thao tác' }}</mat-header-cell>
            <mat-cell *matCellDef="let t">
              @if (t.statusCode === 'collected') {
              <button mat-stroked-button color="primary"
                      (click)="openResultForm(t)" [attr.aria-label]="'labOrders.enterResultLabel' | hhTranslate:'Nhập kết quả'">
                <mat-icon>edit_note</mat-icon> {{ 'labOrders.enterResult' | hhTranslate:'Nhập KQ' }}
              </button>
              }
            </mat-cell>
          </ng-container>

          <mat-header-row *matHeaderRowDef="testColumns"></mat-header-row>
          <mat-row *matRowDef="let row; columns: testColumns;"></mat-row>
        </mat-table>
        }

        @if (labOrder.tests.length === 0) {
        <p class="empty">{{ 'labOrders.empty.tests' | hhTranslate:'Chưa có xét nghiệm nào.' }}</p>
        }
      </hh-page-section>

      <!-- Result recording form -->
      @if (selectedTest) {
      <hh-page-section [title]="'labOrders.section.result' | hhTranslate:'Nhập kết quả: {{test}}':{test: selectedTest.testName}">
        <form [formGroup]="resultForm" (ngSubmit)="recordResult()" class="result-form">
          <div class="form-row">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'labOrders.field.value' | hhTranslate:'Giá trị' }}</mat-label>
              <input matInput formControlName="value" required [attr.aria-label]="'labOrders.field.valueLabel' | hhTranslate:'Giá trị kết quả'">
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'labOrders.field.unit' | hhTranslate:'Đơn vị' }}</mat-label>
              <input matInput formControlName="unit" required [attr.aria-label]="'labOrders.field.unitLabel' | hhTranslate:'Đơn vị đo'">
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'labOrders.field.referenceRange' | hhTranslate:'Khoảng tham chiếu' }}</mat-label>
              <input matInput formControlName="referenceRange" [attr.aria-label]="'labOrders.field.referenceRangeLabel' | hhTranslate:'Khoảng tham chiếu'">
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'labOrders.field.abnormal' | hhTranslate:'Bất thường' }}</mat-label>
              <mat-select formControlName="abnormalFlagCode" [attr.aria-label]="'labOrders.field.abnormalLabel' | hhTranslate:'Mức độ bất thường'">
                <mat-option value="none">{{ 'labOrders.abnormal.none' | hhTranslate:'Bình thường' }}</mat-option>
                <mat-option value="low">{{ 'labOrders.abnormal.low' | hhTranslate:'Thấp' }}</mat-option>
                <mat-option value="high">{{ 'labOrders.abnormal.high' | hhTranslate:'Cao' }}</mat-option>
                <mat-option value="critically_low">{{ 'labOrders.abnormal.criticallyLow' | hhTranslate:'Thấp nghiêm trọng' }}</mat-option>
                <mat-option value="critically_high">{{ 'labOrders.abnormal.criticallyHigh' | hhTranslate:'Cao nghiêm trọng' }}</mat-option>
                <mat-option value="abnormal">{{ 'labOrders.abnormal.abnormal' | hhTranslate:'Bất thường' }}</mat-option>
              </mat-select>
            </mat-form-field>
          </div>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>{{ 'labOrders.field.notes' | hhTranslate:'Ghi chú' }}</mat-label>
            <textarea matInput formControlName="notes" rows="2" [attr.aria-label]="'labOrders.field.notesLabel' | hhTranslate:'Ghi chú kết quả'"></textarea>
          </mat-form-field>

          <div class="form-actions">
            <button mat-button type="button" (click)="cancelResultForm()">{{ 'common.cancel' | hhTranslate:'Hủy' }}</button>
            <button mat-raised-button color="primary" type="submit"
                    [disabled]="resultForm.invalid || recordingResult">
              @if (recordingResult) {
              <mat-spinner diameter="18" class="btn-spinner"></mat-spinner>
              }
              {{ recordingResult ? ('labOrders.saving' | hhTranslate:'Đang lưu...') : ('labOrders.saveResult' | hhTranslate:'Lưu kết quả') }}
            </button>
          </div>
        </form>
      </hh-page-section>
      }
      }
    </hh-page-layout>
  `,
    styles: [`
    .meta-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
    }

    .critical-alert-row { display: flex; justify-content: space-between; gap: 16px; padding: 12px 0; border-top: 1px solid #EAEAEA; }
    .critical-alert-row:first-child { border-top: 0; }
    .critical-alert-title { margin: 0; font-weight: 600; }
    .critical-alert-meta { margin: 4px 0 0; color: #787774; font-size: 13px; }

    .abnormal { color: var(--pastel-red-text, #C25450); font-weight: 500; }
    .abnormal-flag { color: #c62828; font-size: 11px; font-weight: 500; }

    .result-form { display: flex; flex-direction: column; gap: 16px; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .full-width { width: 100%; }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
    .empty { color: #999; font-style: italic; padding: 12px; }
  `],
})
export class LabOrderDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  labOrder?: LabOrder;
  loadError = false;
  loading = true;
  errorMessage = '';
  selectedTest: LabTest | null = null;
  recordingResult = false;
  testColumns = ['testName', 'specimenType', 'statusName', 'result', 'actions'];
  criticalAlerts: CriticalAlert[] = [];
  private labOrderId = '';

  resultForm = this.fb.group({
    value: ['', Validators.required],
    unit: ['', Validators.required],
    referenceRange: [''],
    abnormalFlagCode: ['none'],
    notes: [''],
  });

  constructor(
    private route: ActivatedRoute,
    private labService: LabService,
    private fb: FormBuilder,
    private snackBar: MatSnackBar,
    private router: Router,
    private cdr: ChangeDetectorRef,
    private criticalAlertService: LabCriticalAlertService,
  ) {}

  ngOnInit(): void {
    this.labOrderId = this.route.snapshot.params['id'];
    this.loadLabOrder();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    this.router.navigate(['/lab']);
  }

  loadLabOrder(): void {
    this.loadError = false;
    this.loading = true;
    this.labService.getLabOrder(this.labOrderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (order) => {
          this.labOrder = order;
          this.loadCriticalAlerts();
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.loading = false;
          this.errorMessage = this.i18n.t('labOrders.loadFailed', 'Không thể tải thông tin phiếu xét nghiệm.');
          this.cdr.markForCheck();
        },
      });
  }

  submitLabOrder(): void {
    if (!this.labOrder) return;
    this.labService.submitLabOrder(this.labOrder.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('labOrders.submitSuccess', 'Đã gửi phiếu xét nghiệm'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadLabOrder();
        },
        error: () => this.snackBar.open(this.i18n.t('labOrders.submitFailed', 'Không thể gửi phiếu xét nghiệm'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }

  collectSpecimen(): void {
    if (!this.labOrder) return;
    this.labService.collectSpecimen(this.labOrder.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('labOrders.collectSuccess', 'Đã ghi nhận lấy mẫu bệnh phẩm'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadLabOrder();
        },
        error: () => this.snackBar.open(this.i18n.t('labOrders.collectFailed', 'Không thể ghi nhận lấy mẫu'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }

  openResultForm(test: LabTest): void {
    this.selectedTest = test;
    this.resultForm.reset({ abnormalFlagCode: 'none' });
    this.cdr.markForCheck();
  }

  cancelResultForm(): void {
    this.selectedTest = null;
    this.resultForm.reset();
    this.cdr.markForCheck();
  }

  recordResult(): void {
    if (this.resultForm.invalid || !this.selectedTest) return;

    this.recordingResult = true;
    const data = this.resultForm.value as any;

    this.labService.recordResult(this.selectedTest.id, data)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('labOrders.resultSaved', 'Đã lưu kết quả xét nghiệm'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.selectedTest = null;
          this.resultForm.reset();
          this.recordingResult = false;
          this.loadLabOrder();
        },
        error: () => {
          this.recordingResult = false;
          this.snackBar.open(this.i18n.t('labOrders.resultSaveFailed', 'Không thể lưu kết quả'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  cancelLabOrder(): void {
    if (!this.labOrder) return;
    this.labService.cancelLabOrder(this.labOrder.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('labOrders.cancelSuccess', 'Đã hủy phiếu xét nghiệm'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadLabOrder();
        },
        error: () => this.snackBar.open(this.i18n.t('labOrders.cancelFailed', 'Không thể hủy phiếu xét nghiệm'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }

  acknowledgeCriticalAlert(alert: CriticalAlert): void {
    this.criticalAlertService.acknowledgeCriticalAlert(alert.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notify(this.i18n.t('labOrders.alertAcknowledged', 'Đã ghi nhận cảnh báo nghiêm trọng'));
          this.loadCriticalAlerts();
        },
      });
  }

  private loadCriticalAlerts(): void {
    if (!this.labOrderId) {
      return;
    }

    this.criticalAlertService.listCriticalAlerts()
      .pipe(takeUntil(this.destroy$))
      .subscribe((alerts) => {
        this.criticalAlerts = alerts.filter((alert) => alert.labOrderId === this.labOrderId);
        this.cdr.markForCheck();
      });
  }

  private notify(message: string): void {
    this.snackBar.open(message, this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
  }
}
