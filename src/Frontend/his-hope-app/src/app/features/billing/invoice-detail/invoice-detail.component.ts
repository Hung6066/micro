import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, takeUntil } from 'rxjs';
import { BillingService } from '@core/services/billing.service';
import { Invoice } from '@core/models/invoice.model';

@Component({
    selector: 'app-invoice-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatCardModule, MatIconModule, MatButtonModule, MatTableModule,
        MatFormFieldModule, MatInputModule, MatSelectModule,
        MatDatepickerModule, MatNativeDateModule, MatProgressSpinnerModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    @if (invoice) {
    <div class="invoice-detail">
      <div class="header">
        <div>
          <h1>Hóa đơn #{{ invoice.invoiceNumber }}</h1>
          <p class="subtitle">
            <span class="status-badge" [class.status-draft]="invoice.statusCode === 'draft'"
                  [class.status-issued]="invoice.statusCode === 'issued'"
                  [class.status-partial]="invoice.statusCode === 'partially_paid'"
                  [class.status-paid]="invoice.statusCode === 'paid'"
                  [class.status-overdue]="invoice.statusCode === 'overdue'"
                  [class.status-cancelled]="invoice.statusCode === 'cancelled'"
                  [class.status-voided]="invoice.statusCode === 'voided'">
              {{ invoice.statusName }}
            </span>
          </p>
        </div>
        <div class="header-actions">
          @if (invoice.statusCode !== 'paid' && invoice.statusCode !== 'cancelled' && invoice.statusCode !== 'voided') {
          <button mat-raised-button color="primary"
                  (click)="showPaymentForm = !showPaymentForm"
                  aria-label="Ghi nhận thanh toán">
            <mat-icon>payment</mat-icon> Ghi nhận thanh toán
          </button>
          }
          @if (invoice.statusCode !== 'voided' && invoice.statusCode !== 'cancelled' && invoice.statusCode !== 'paid') {
          <button mat-stroked-button color="warn"
                  (click)="voidInvoice()"
                  aria-label="Hủy hóa đơn">
            <mat-icon>cancel</mat-icon> Hủy hóa đơn
          </button>
          }
        </div>
      </div>

      <!-- Invoice summary -->
      <div class="summary-grid">
        <mat-card>
          <mat-card-header><mat-card-title>Thông tin hóa đơn</mat-card-title></mat-card-header>
          <mat-card-content>
            <p><strong>Bệnh nhân:</strong> {{ invoice.patientName || invoice.patientId }}</p>
            @if (invoice.encounterId) {
            <p><strong>Mã hồ sơ:</strong> {{ invoice.encounterId | slice:0:8 }}...</p>
            }
            <p><strong>Ngày hóa đơn:</strong> {{ invoice.invoiceDate | date:'medium' }}</p>
            @if (invoice.dueDate) {
            <p><strong>Ngày đến hạn:</strong> {{ invoice.dueDate | date:'medium' }}</p>
            }
            @if (invoice.notes) {
            <p><strong>Ghi chú:</strong> {{ invoice.notes }}</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header><mat-card-title>Tổng kết</mat-card-title></mat-card-header>
          <mat-card-content class="totals">
            <div class="total-row"><span>Tạm tính:</span><span>{{ invoice.subTotal | number:'1.0-0' }} đ</span></div>
            @if (invoice.taxAmount) {
            <div class="total-row"><span>Thuế:</span><span>{{ invoice.taxAmount | number:'1.0-0' }} đ</span></div>
            }
            @if (invoice.discountAmount) {
            <div class="total-row"><span>Giảm giá:</span><span>-{{ invoice.discountAmount | number:'1.0-0' }} đ</span></div>
            }
            <div class="total-row grand-total"><span>Tổng cộng:</span><span>{{ invoice.totalAmount | number:'1.0-0' }} đ</span></div>
            <div class="total-row paid"><span>Đã thanh toán:</span><span>{{ invoice.paidAmount | number:'1.0-0' }} đ</span></div>
            <div class="total-row balance" [class.text-danger]="invoice.balanceDue > 0">
              <span>Còn nợ:</span><span>{{ invoice.balanceDue | number:'1.0-0' }} đ</span>
            </div>
          </mat-card-content>
        </mat-card>
      </div>

      <!-- Line Items -->
      <mat-card class="items-card">
        <mat-card-header><mat-card-title>Chi tiết dịch vụ ({{ invoice.lineItems.length }})</mat-card-title></mat-card-header>
        <mat-card-content>
          @if (invoice.lineItems.length > 0) {
          <table class="items-table">
            <thead>
              <tr>
                <th>Mã</th>
                <th>Mô tả</th>
                <th>Loại</th>
                <th class="num">SL</th>
                <th class="num">Đơn giá</th>
                <th class="num">Thành tiền</th>
              </tr>
            </thead>
            <tbody>
              @for (item of invoice.lineItems; track item.itemCode) {
              <tr>
                <td>{{ item.itemCode }}</td>
                <td>{{ item.description }}</td>
                <td>{{ item.itemTypeName }}</td>
                <td class="num">{{ item.quantity }}</td>
                <td class="num">{{ item.unitPrice | number:'1.0-0' }} đ</td>
                <td class="num">{{ item.amount | number:'1.0-0' }} đ</td>
              </tr>
              }
            </tbody>
          </table>
          }
          @if (invoice.lineItems.length === 0) {
          <p class="empty">Không có dịch vụ nào.</p>
          }
        </mat-card-content>
      </mat-card>

      <!-- Payment History -->
      <mat-card class="payments-card">
        <mat-card-header><mat-card-title>Lịch sử thanh toán ({{ invoice.payments.length }})</mat-card-title></mat-card-header>
        <mat-card-content>
          @if (invoice.payments.length > 0) {
          <mat-table [dataSource]="invoice.payments">
            <ng-container matColumnDef="amount">
              <mat-header-cell *matHeaderCellDef>Số tiền</mat-header-cell>
              <mat-cell *matCellDef="let p">{{ p.amount | number:'1.0-0' }} đ</mat-cell>
            </ng-container>

            <ng-container matColumnDef="paymentDate">
              <mat-header-cell *matHeaderCellDef>Ngày</mat-header-cell>
              <mat-cell *matCellDef="let p">{{ p.paymentDate | date:'medium' }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="methodName">
              <mat-header-cell *matHeaderCellDef>Phương thức</mat-header-cell>
              <mat-cell *matCellDef="let p">{{ p.methodName }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="referenceNumber">
              <mat-header-cell *matHeaderCellDef>Mã tham chiếu</mat-header-cell>
              <mat-cell *matCellDef="let p">{{ p.referenceNumber || '-' }}</mat-cell>
            </ng-container>

            <mat-header-row *matHeaderRowDef="paymentColumns"></mat-header-row>
            <mat-row *matRowDef="let row; columns: paymentColumns;"></mat-row>
          </mat-table>
          }
          @if (invoice.payments.length === 0) {
          <p class="empty">Chưa có thanh toán nào.</p>
          }
        </mat-card-content>
      </mat-card>

      <!-- Payment Form -->
      @if (showPaymentForm) {
      <mat-card class="payment-form-card">
        <mat-card-header><mat-card-title>Ghi nhận thanh toán</mat-card-title></mat-card-header>
        <mat-card-content>
          <form [formGroup]="paymentForm" (ngSubmit)="recordPayment()" class="payment-form">
            <div class="form-row">
              <mat-form-field appearance="outline">
                <mat-label>Số tiền</mat-label>
                <input matInput formControlName="amount" type="number" required min="1"
                       [attr.max]="invoice.balanceDue" aria-label="Số tiền thanh toán">
                <span matSuffix>đ</span>
                @if (paymentForm.get('amount')?.hasError('required')) {
                <mat-error>Vui lòng nhập số tiền</mat-error>
                }
                @if (paymentForm.get('amount')?.hasError('min')) {
                <mat-error>Số tiền phải lớn hơn 0</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Ngày thanh toán</mat-label>
                <input matInput [matDatepicker]="payPicker" formControlName="paymentDate" required
                       aria-label="Ngày thanh toán">
                <mat-datepicker-toggle matSuffix [for]="payPicker"></mat-datepicker-toggle>
                <mat-datepicker #payPicker></mat-datepicker>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Phương thức</mat-label>
                <mat-select formControlName="methodCode" required aria-label="Phương thức thanh toán">
                  <mat-option value="cash">Tiền mặt</mat-option>
                  <mat-option value="credit_card">Thẻ tín dụng</mat-option>
                  <mat-option value="debit_card">Thẻ ghi nợ</mat-option>
                  <mat-option value="bank_transfer">Chuyển khoản</mat-option>
                  <mat-option value="insurance">Bảo hiểm</mat-option>
                  <mat-option value="mobile_payment">Ví điện tử</mat-option>
                  <mat-option value="other">Khác</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Mã tham chiếu</mat-label>
                <input matInput formControlName="referenceNumber" aria-label="Mã tham chiếu">
              </mat-form-field>
            </div>

            <div class="form-actions">
              <button mat-button type="button" (click)="showPaymentForm = false">Hủy</button>
              <button mat-raised-button color="primary" type="submit"
                      [disabled]="paymentForm.invalid || recordingPayment">
                @if (recordingPayment) {
                <mat-spinner diameter="18" class="btn-spinner"></mat-spinner>
                }
                {{ recordingPayment ? 'Đang lưu...' : 'Xác nhận thanh toán' }}
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
      }
    </div>
    }

    @if (!invoice && !loadError) {
    <div class="loading-container">
      <mat-spinner diameter="40" aria-label="Đang tải"></mat-spinner>
      <p>Đang tải thông tin hóa đơn...</p>
    </div>
    }

    @if (loadError) {
    <div class="error-container">
      <mat-icon color="warn">error_outline</mat-icon>
      <p>Không thể tải thông tin hóa đơn. Vui lòng thử lại sau.</p>
      <button mat-stroked-button color="primary" (click)="loadInvoice()">Thử lại</button>
    </div>
    }
  `,
    styles: [`
    .invoice-detail { padding: 24px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .header-actions { display: flex; gap: 12px; flex-wrap: wrap; }
    .subtitle { color: #666; font-size: 14px; }
    .status-badge { padding: 4px 16px; border-radius: 16px; font-weight: 500; font-size: 14px; display: inline-block; }
    .status-draft { background: #e0e0e0; color: #616161; }
    .status-issued { background: #e3f2fd; color: #1565c0; }
    .status-partial { background: #fff3e0; color: #e65100; }
    .status-paid { background: #e8f5e9; color: #2e7d32; }
    .status-overdue { background: #fbe9e7; color: #c62828; }
    .status-cancelled { background: #fce4ec; color: #b71c1c; }
    .status-voided { background: #f3e5f5; color: #6a1b9a; }
    .summary-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 20px; }
    .totals { display: flex; flex-direction: column; gap: 8px; }
    .total-row { display: flex; justify-content: space-between; padding: 4px 0; }
    .grand-total { font-weight: 700; font-size: 16px; border-top: 2px solid #e0e0e0; padding-top: 8px; margin-top: 4px; }
    .paid { color: #2e7d32; }
    .balance { font-weight: 500; }
    .text-danger { color: #c62828; }
    mat-card-content p { margin: 8px 0; }
    mat-table { width: 100%; }
    .items-card, .payments-card, .payment-form-card { margin-bottom: 20px; }
    .items-table { width: 100%; border-collapse: collapse; }
    .items-table th, .items-table td { padding: 8px 12px; text-align: left; border-bottom: 1px solid #e0e0e0; }
    .items-table th { background: #f5f5f5; font-weight: 500; }
    .items-table .num { text-align: right; }
    .payment-form { display: flex; flex-direction: column; gap: 16px; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
    .empty { color: #999; font-style: italic; padding: 12px; }
    .loading-container, .error-container { display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 64px 24px; gap: 16px; color: #666; }
  `],
})
export class InvoiceDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  invoice?: Invoice;
  loadError = false;
  showPaymentForm = false;
  recordingPayment = false;
  paymentColumns = ['amount', 'paymentDate', 'methodName', 'referenceNumber'];
  private invoiceId = '';

  paymentForm = this.fb.group({
    amount: [0, [Validators.required, Validators.min(1)]],
    paymentDate: [new Date().toISOString(), Validators.required],
    methodCode: ['cash', Validators.required],
    referenceNumber: [''],
  });

  constructor(
    private route: ActivatedRoute,
    private billingService: BillingService,
    private fb: FormBuilder,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.invoiceId = this.route.snapshot.params['id'];
    this.loadInvoice();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadInvoice(): void {
    this.loadError = false;
    this.billingService.getInvoice(this.invoiceId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (inv) => {
          this.invoice = inv;
          this.paymentForm.patchValue({ amount: inv.balanceDue, paymentDate: new Date().toISOString() });
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.cdr.markForCheck();
        },
      });
  }

  recordPayment(): void {
    if (this.paymentForm.invalid || !this.invoice) return;

    this.recordingPayment = true;
    const data = this.paymentForm.value as any;

    this.billingService.recordPayment(this.invoice.id, data)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã ghi nhận thanh toán thành công', 'Đóng', { duration: 3000 });
          this.showPaymentForm = false;
          this.recordingPayment = false;
          this.paymentForm.reset({ methodCode: 'cash' });
          this.loadInvoice();
        },
        error: () => {
          this.recordingPayment = false;
          this.snackBar.open('Không thể ghi nhận thanh toán', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  voidInvoice(): void {
    if (!this.invoice) return;
    this.billingService.voidInvoice(this.invoice.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã hủy hóa đơn', 'Đóng', { duration: 3000 });
          this.loadInvoice();
        },
        error: () => this.snackBar.open('Không thể hủy hóa đơn', 'Đóng', { duration: 5000 }),
      });
  }
}
