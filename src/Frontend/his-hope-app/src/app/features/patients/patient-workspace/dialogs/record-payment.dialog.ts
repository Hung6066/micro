import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { BillingService } from '@core/services/billing.service';
import { Invoice } from '@core/models/invoice.model';

export interface RecordPaymentData {
  patientId: string;
  patientName: string;
  invoices: Invoice[];
}

@Component({
  selector: 'app-record-payment-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Ghi nhận thanh toán</h2>
    <mat-dialog-content>
      <div class="patient-info" *ngIf="data.patientName">
        <mat-icon>person</mat-icon>
        <span>{{ data.patientName }}</span>
      </div>

      <div *ngIf="data.invoices.length === 0" class="no-invoices">
        <mat-icon>receipt</mat-icon>
        <p>Bệnh nhân không có hóa đơn nào cần thanh toán</p>
      </div>

      <form [formGroup]="form" class="dialog-form" *ngIf="data.invoices.length > 0">
        <mat-form-field appearance="outline">
          <mat-label>Chọn hóa đơn</mat-label>
          <mat-select formControlName="invoiceId" required>
            <mat-option *ngFor="let inv of payableInvoices" [value]="inv.id">
              {{ inv.invoiceNumber }} - Còn: {{ inv.balanceDue | number }}₫
            </mat-option>
          </mat-select>
          <mat-error *ngIf="form.get('invoiceId')?.hasError('required')">Vui lòng chọn hóa đơn</mat-error>
        </mat-form-field>

        <div class="balance-info" *ngIf="selectedInvoice">
          <p><strong>Số hóa đơn:</strong> {{ selectedInvoice.invoiceNumber }}</p>
          <p><strong>Tổng tiền:</strong> {{ selectedInvoice.totalAmount | number }}₫</p>
          <p><strong>Đã thanh toán:</strong> {{ selectedInvoice.paidAmount | number }}₫</p>
          <p><strong>Còn nợ:</strong> {{ selectedInvoice.balanceDue | number }}₫</p>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>Số tiền thanh toán</mat-label>
          <input matInput type="number" formControlName="amount" min="1" required>
          <span matTextSuffix>₫</span>
          <mat-error *ngIf="form.get('amount')?.hasError('required')">Vui lòng nhập số tiền</mat-error>
          <mat-error *ngIf="form.get('amount')?.hasError('min')">Số tiền phải > 0</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Phương thức thanh toán</mat-label>
          <mat-select formControlName="methodCode" required>
            <mat-option value="cash">Tiền mặt</mat-option>
            <mat-option value="credit_card">Thẻ tín dụng</mat-option>
            <mat-option value="debit_card">Thẻ ghi nợ</mat-option>
            <mat-option value="bank_transfer">Chuyển khoản</mat-option>
            <mat-option value="insurance">Bảo hiểm</mat-option>
            <mat-option value="mobile_payment">Ví điện tử</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Số tham chiếu (nếu có)</mat-label>
          <input matInput formControlName="referenceNumber" placeholder="Mã giao dịch...">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Đóng</button>
      <button mat-raised-button color="primary" (click)="save()"
              [disabled]="form.invalid || saving || data.invoices.length === 0">
        <mat-icon>payments</mat-icon>
        <span *ngIf="!saving">Ghi nhận thanh toán</span>
        <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .patient-info { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; padding: 8px 12px; background: #fce4ec; border-radius: 8px; color: #c62828; font-weight: 500; }
    .no-invoices { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 24px; color: #999; }
    .no-invoices mat-icon { font-size: 48px; width: 48px; height: 48px; }
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 380px; }
    .balance-info { background: #f5f5f5; padding: 12px; border-radius: 8px; border-left: 4px solid #ffa726; }
    .balance-info p { margin: 4px 0; font-size: 14px; }
  `],
})
export class RecordPaymentDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  form: FormGroup;
  saving = false;

  get payableInvoices(): Invoice[] {
    return this.data.invoices.filter(i => i.balanceDue > 0);
  }

  get selectedInvoice(): Invoice | undefined {
    const id = this.form.get('invoiceId')?.value;
    return this.data.invoices.find(i => i.id === id);
  }

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<RecordPaymentDialogComponent>,
    private billingService: BillingService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: RecordPaymentData,
  ) {
    this.form = this.fb.group({
      invoiceId: ['', Validators.required],
      amount: ['', [Validators.required, Validators.min(1)]],
      methodCode: ['cash', Validators.required],
      referenceNumber: [''],
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  save(): void {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    this.cdr.markForCheck();

    this.billingService.recordPayment(this.form.value.invoiceId, {
      amount: this.form.value.amount,
      paymentDate: new Date().toISOString(),
      methodCode: this.form.value.methodCode,
      referenceNumber: this.form.value.referenceNumber || undefined,
    }).pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã ghi nhận thanh toán', 'Đóng', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: () => {
          this.saving = false;
          this.snackBar.open('Không thể ghi nhận thanh toán', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
