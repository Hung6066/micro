import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, Validators } from '@angular/forms';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';
import { LabService } from '@core/services/lab.service';
import { LabOrder, LabTest, AbnormalFlag } from '@core/models/lab-order.model';

@Component({
    selector: 'app-lab-order-detail',
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="lab-order-detail" *ngIf="labOrder">
      <div class="header">
        <div>
          <h1>Phiếu xét nghiệm #{{ labOrder.id | slice:0:8 }}...</h1>
          <p class="subtitle">
            <span class="status-badge" [class.status-ordered]="labOrder.statusCode === 'ordered'"
                  [class.status-collected]="labOrder.statusCode === 'specimen_collected'"
                  [class.status-progress]="labOrder.statusCode === 'in_progress'"
                  [class.status-completed]="labOrder.statusCode === 'completed'"
                  [class.status-cancelled]="labOrder.statusCode === 'cancelled'">
              {{ labOrder.statusName }}
            </span>
            <span class="priority-badge" [class.priority-high]="labOrder.priorityCode === 'high'"
                  [class.priority-urgent]="labOrder.priorityCode === 'urgent'"
                  [class.priority-routine]="labOrder.priorityCode === 'routine'" style="margin-left: 8px;">
              {{ labOrder.priorityName }}
            </span>
          </p>
        </div>
        <div class="header-actions">
          <button mat-raised-button color="primary"
                  *ngIf="labOrder.statusCode === 'ordered'"
                  (click)="submitLabOrder()"
                  attr.aria-label="Gửi phiếu xét nghiệm">
            <mat-icon>send</mat-icon> Gửi phiếu
          </button>
          <button mat-raised-button color="accent"
                  *ngIf="labOrder.statusCode === 'ordered' || labOrder.statusCode === 'in_progress'"
                  (click)="collectSpecimen()"
                  attr.aria-label="Lấy mẫu bệnh phẩm">
            <mat-icon>science</mat-icon> Lấy mẫu
          </button>
          <button mat-stroked-button color="warn"
                  *ngIf="labOrder.statusCode !== 'completed' && labOrder.statusCode !== 'cancelled'"
                  (click)="cancelLabOrder()"
                  attr.aria-label="Hủy phiếu xét nghiệm">
            <mat-icon>cancel</mat-icon> Hủy phiếu
          </button>
        </div>
      </div>

      <div class="detail-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Thông tin phiếu</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Bệnh nhân:</strong> {{ labOrder.patientName || labOrder.patientId }}</p>
            <p><strong>Bác sĩ:</strong> {{ labOrder.providerName || labOrder.providerId }}</p>
            <p *ngIf="labOrder.encounterId"><strong>Mã hồ sơ:</strong> {{ labOrder.encounterId | slice:0:8 }}...</p>
            <p><strong>Ngày chỉ định:</strong> {{ labOrder.orderDate | date:'medium' }}</p>
            <p *ngIf="labOrder.notes"><strong>Ghi chú:</strong> {{ labOrder.notes }}</p>
          </mat-card-content>
        </mat-card>
      </div>

      <mat-card class="tests-card">
        <mat-card-header>
          <mat-card-title>Danh sách xét nghiệm ({{ labOrder.tests.length }})</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <mat-table [dataSource]="labOrder.tests" *ngIf="labOrder.tests.length > 0">
            <ng-container matColumnDef="testName">
              <mat-header-cell *matHeaderCellDef>Xét nghiệm</mat-header-cell>
              <mat-cell *matCellDef="let t">{{ t.testName }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="specimenType">
              <mat-header-cell *matHeaderCellDef>Loại mẫu</mat-header-cell>
              <mat-cell *matCellDef="let t">{{ t.specimenType }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="statusName">
              <mat-header-cell *matHeaderCellDef>Trạng thái</mat-header-cell>
              <mat-cell *matCellDef="let t">
                <span class="status-badge" [class.status-ordered]="t.statusCode === 'ordered'"
                      [class.status-collected]="t.statusCode === 'collected'"
                      [class.status-completed]="t.statusCode === 'completed'">
                  {{ t.statusName }}
                </span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="result">
              <mat-header-cell *matHeaderCellDef>Kết quả</mat-header-cell>
              <mat-cell *matCellDef="let t">
                <ng-container *ngIf="t.result; else noResult">
                  <span [class.abnormal]="t.result.abnormalFlagCode !== 'none'">
                    {{ t.result.value }} {{ t.result.unit }}
                  </span>
                  <span class="abnormal-flag" *ngIf="t.result.abnormalFlagCode !== 'none'">
                    ({{ t.result.abnormalFlagName }})
                  </span>
                </ng-container>
                <ng-template #noResult>-</ng-template>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="actions">
              <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
              <mat-cell *matCellDef="let t">
                <button mat-stroked-button color="primary" *ngIf="t.statusCode === 'collected'"
                        (click)="openResultForm(t)" attr.aria-label="Nhập kết quả cho {{ t.testName }}">
                  <mat-icon>edit_note</mat-icon> Nhập KQ
                </button>
              </mat-cell>
            </ng-container>

            <mat-header-row *matHeaderRowDef="testColumns"></mat-header-row>
            <mat-row *matRowDef="let row; columns: testColumns;"></mat-row>
          </mat-table>

          <p class="empty" *ngIf="labOrder.tests.length === 0">Chưa có xét nghiệm nào.</p>
        </mat-card-content>
      </mat-card>

      <!-- Result recording form -->
      <mat-card class="result-form-card" *ngIf="selectedTest">
        <mat-card-header>
          <mat-card-title>Nhập kết quả: {{ selectedTest.testName }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="resultForm" (ngSubmit)="recordResult()" class="result-form">
            <div class="form-row">
              <mat-form-field appearance="outline">
                <mat-label>Giá trị</mat-label>
                <input matInput formControlName="value" required aria-label="Giá trị kết quả">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Đơn vị</mat-label>
                <input matInput formControlName="unit" required aria-label="Đơn vị đo">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Khoảng tham chiếu</mat-label>
                <input matInput formControlName="referenceRange" aria-label="Khoảng tham chiếu">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Bất thường</mat-label>
                <mat-select formControlName="abnormalFlagCode" aria-label="Mức độ bất thường">
                  <mat-option value="none">Bình thường</mat-option>
                  <mat-option value="low">Thấp</mat-option>
                  <mat-option value="high">Cao</mat-option>
                  <mat-option value="critically_low">Thấp nghiêm trọng</mat-option>
                  <mat-option value="critically_high">Cao nghiêm trọng</mat-option>
                  <mat-option value="abnormal">Bất thường</mat-option>
                </mat-select>
              </mat-form-field>
            </div>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Ghi chú</mat-label>
              <textarea matInput formControlName="notes" rows="2" aria-label="Ghi chú kết quả"></textarea>
            </mat-form-field>

            <div class="form-actions">
              <button mat-button type="button" (click)="cancelResultForm()">Hủy</button>
              <button mat-raised-button color="primary" type="submit"
                      [disabled]="resultForm.invalid || recordingResult">
                <mat-spinner diameter="18" *ngIf="recordingResult" class="btn-spinner"></mat-spinner>
                {{ recordingResult ? 'Đang lưu...' : 'Lưu kết quả' }}
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>

    <div class="loading-container" *ngIf="!labOrder && !loadError">
      <mat-spinner diameter="40" aria-label="Đang tải"></mat-spinner>
      <p>Đang tải thông tin phiếu xét nghiệm...</p>
    </div>

    <div class="error-container" *ngIf="loadError">
      <mat-icon color="warn">error_outline</mat-icon>
      <p>Không thể tải thông tin phiếu xét nghiệm.</p>
      <button mat-stroked-button color="primary" (click)="loadLabOrder()">Thử lại</button>
    </div>
  `,
    styles: [`
    .lab-order-detail { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .header-actions { display: flex; gap: 12px; flex-wrap: wrap; }
    .subtitle { color: #666; font-size: 14px; display: flex; align-items: center; }
    .detail-grid { display: grid; grid-template-columns: 1fr; gap: 20px; margin-bottom: 20px; }
    .tests-card { margin-bottom: 20px; }
    mat-card-content p { margin: 8px 0; }
    mat-table { width: 100%; }
    .abnormal { color: var(--pastel-red-text, #C25450); font-weight: 500; }
    .abnormal-flag { color: #c62828; font-size: 11px; font-weight: 500; }
    .result-form-card { margin-bottom: 20px; }
    .result-form { display: flex; flex-direction: column; gap: 16px; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .full-width { width: 100%; }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
    .empty { color: #999; font-style: italic; padding: 12px; }
    .loading-container, .error-container { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 64px 24px; gap: 16px; color: #666; }
  `],
    standalone: false
})
export class LabOrderDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  labOrder?: LabOrder;
  loadError = false;
  selectedTest: LabTest | null = null;
  recordingResult = false;
  testColumns = ['testName', 'specimenType', 'statusName', 'result', 'actions'];
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
  ) {}

  ngOnInit(): void {
    this.labOrderId = this.route.snapshot.params['id'];
    this.loadLabOrder();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadLabOrder(): void {
    this.loadError = false;
    this.labService.getLabOrder(this.labOrderId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (order) => {
          this.labOrder = order;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
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
          this.snackBar.open('Đã gửi phiếu xét nghiệm', 'Đóng', { duration: 3000 });
          this.loadLabOrder();
        },
        error: () => this.snackBar.open('Không thể gửi phiếu xét nghiệm', 'Đóng', { duration: 5000 }),
      });
  }

  collectSpecimen(): void {
    if (!this.labOrder) return;
    this.labService.collectSpecimen(this.labOrder.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã ghi nhận lấy mẫu bệnh phẩm', 'Đóng', { duration: 3000 });
          this.loadLabOrder();
        },
        error: () => this.snackBar.open('Không thể ghi nhận lấy mẫu', 'Đóng', { duration: 5000 }),
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
          this.snackBar.open('Đã lưu kết quả xét nghiệm', 'Đóng', { duration: 3000 });
          this.selectedTest = null;
          this.resultForm.reset();
          this.recordingResult = false;
          this.loadLabOrder();
        },
        error: () => {
          this.recordingResult = false;
          this.snackBar.open('Không thể lưu kết quả', 'Đóng', { duration: 5000 });
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
          this.snackBar.open('Đã hủy phiếu xét nghiệm', 'Đóng', { duration: 3000 });
          this.loadLabOrder();
        },
        error: () => this.snackBar.open('Không thể hủy phiếu xét nghiệm', 'Đóng', { duration: 5000 }),
      });
  }
}
