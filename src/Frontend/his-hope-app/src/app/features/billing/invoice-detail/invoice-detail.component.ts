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
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Subject, takeUntil } from 'rxjs';
import { BillingService } from '@core/services/billing.service';
import { Invoice } from '@core/models/invoice.model';
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
    selector: 'app-invoice-detail',
    standalone: true,
    imports: [
        CommonModule, RouterModule, ReactiveFormsModule,
        MatIconModule, MatButtonModule, MatTableModule,
        MatFormFieldModule, MatInputModule, MatSelectModule,
        MatDatepickerModule, MatNativeDateModule, MatProgressSpinnerModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent, HisHopePageSectionComponent,
        HisHopeStateComponent, HisHopeStatusBadgeComponent, HisHopeMetaItemComponent,
        HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'invoices.detail' | hhTranslate:'Chi tiết hóa đơn'"
                      [subtitle]="invoice ? ('invoices.detailSubtitle' | hhTranslate:'Hóa đơn #{{number}} | Bệnh nhân: {{name}}':{number: invoice.invoiceNumber, name: (invoice.patientName || invoice.patientId)}) : ''">
        <button mat-stroked-button (click)="goBack()">
          <mat-icon>arrow_back</mat-icon> {{ 'common.back' | hhTranslate:'Quay lại' }}
        </button>
        @if (invoice) {
        <hh-status-badge [status]="invoice.statusCode" [label]="invoice.statusName" />
        @if (invoice.statusCode !== 'paid' && invoice.statusCode !== 'cancelled' && invoice.statusCode !== 'voided') {
        <button mat-raised-button color="primary"
                (click)="showPaymentForm = !showPaymentForm"
                [attr.aria-label]="'invoices.recordPaymentLabel' | hhTranslate:'Ghi nhận thanh toán'">
          <mat-icon>payment</mat-icon> {{ 'invoices.recordPayment' | hhTranslate:'Ghi nhận thanh toán' }}
        </button>
        }
        @if (invoice.statusCode !== 'voided' && invoice.statusCode !== 'cancelled' && invoice.statusCode !== 'paid') {
        <button mat-stroked-button color="warn"
                (click)="voidInvoice()"
                [attr.aria-label]="'invoices.voidLabel' | hhTranslate:'Hủy hóa đơn'">
          <mat-icon>cancel</mat-icon> {{ 'invoices.void' | hhTranslate:'Hủy hóa đơn' }}
        </button>
        }
        }
      </hh-page-header>

      @if (loading) {
      <hh-state kind="loading" [message]="'common.loading' | hhTranslate" />
      } @else if (loadError) {
      <hh-state kind="error" [message]="errorMessage">
        <button mat-stroked-button (click)="loadInvoice()">{{ 'common.retry' | hhTranslate }}</button>
      </hh-state>
      } @else if (invoice) {
      <!-- Invoice summary -->
      <hh-page-section [title]="'invoices.section.info' | hhTranslate:'Thông tin hóa đơn'">
        <div class="meta-grid">
          <hh-meta-item [label]="'invoices.field.patient' | hhTranslate:'Bệnh nhân'" [value]="invoice.patientName || invoice.patientId" icon="person" />
          @if (invoice.encounterId) {
          <hh-meta-item [label]="'invoices.field.encounter' | hhTranslate:'Mã hồ sơ'" [value]="invoice.encounterId | slice:0:8" icon="folder" />
          }
          <hh-meta-item [label]="'invoices.field.invoiceDate' | hhTranslate:'Ngày hóa đơn'" [value]="(invoice.invoiceDate | date:'medium') || '-'" icon="event" />
          @if (invoice.dueDate) {
          <hh-meta-item [label]="'invoices.field.dueDate' | hhTranslate:'Ngày đến hạn'" [value]="(invoice.dueDate | date:'medium') || '-'" icon="event" />
          }
          @if (invoice.notes) {
          <hh-meta-item [label]="'invoices.field.notes' | hhTranslate:'Ghi chú'" [value]="invoice.notes" icon="notes" />
          }
        </div>
      </hh-page-section>

      <hh-page-section [title]="'invoices.section.totals' | hhTranslate:'Tổng kết'">
        <div class="totals">
          <div class="total-row"><span>{{ 'invoices.field.subtotal' | hhTranslate:'Tạm tính' }}:</span><span>{{ invoice.subTotal | number:'1.0-0' }} đ</span></div>
          @if (invoice.taxAmount) {
          <div class="total-row"><span>{{ 'invoices.field.tax' | hhTranslate:'Thuế' }}:</span><span>{{ invoice.taxAmount | number:'1.0-0' }} đ</span></div>
          }
          @if (invoice.discountAmount) {
          <div class="total-row"><span>{{ 'invoices.field.discount' | hhTranslate:'Giảm giá' }}:</span><span>-{{ invoice.discountAmount | number:'1.0-0' }} đ</span></div>
          }
          <div class="total-row grand-total"><span>{{ 'invoices.field.total' | hhTranslate:'Tổng cộng' }}:</span><span>{{ invoice.totalAmount | number:'1.0-0' }} đ</span></div>
          <div class="total-row paid"><span>{{ 'invoices.field.paid' | hhTranslate:'Đã thanh toán' }}:</span><span>{{ invoice.paidAmount | number:'1.0-0' }} đ</span></div>
          <div class="total-row balance" [class.text-danger]="invoice.balanceDue > 0">
            <span>{{ 'invoices.field.balance' | hhTranslate:'Còn nợ' }}:</span><span>{{ invoice.balanceDue | number:'1.0-0' }} đ</span>
          </div>
        </div>
      </hh-page-section>

      <!-- Line Items -->
      <hh-page-section [title]="'invoices.section.lineItems' | hhTranslate:'Chi tiết dịch vụ ({{count}})':{count: invoice.lineItems.length}">
        @if (invoice.lineItems.length > 0) {
        <table class="items-table">
          <thead>
            <tr>
              <th>{{ 'invoices.column.code' | hhTranslate:'Mã' }}</th>
              <th>{{ 'invoices.column.description' | hhTranslate:'Mô tả' }}</th>
              <th>{{ 'invoices.column.type' | hhTranslate:'Loại' }}</th>
              <th class="num">{{ 'invoices.column.qty' | hhTranslate:'SL' }}</th>
              <th class="num">{{ 'invoices.column.unitPrice' | hhTranslate:'Đơn giá' }}</th>
              <th class="num">{{ 'invoices.column.amount' | hhTranslate:'Thành tiền' }}</th>
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
        <p class="empty">{{ 'invoices.empty.lineItems' | hhTranslate:'Không có dịch vụ nào.' }}</p>
        }
      </hh-page-section>

      <!-- Payment History -->
      <hh-page-section [title]="'invoices.section.payments' | hhTranslate:'Lịch sử thanh toán ({{count}})':{count: invoice.payments.length}">
        @if (invoice.payments.length > 0) {
        <mat-table [dataSource]="invoice.payments">
          <ng-container matColumnDef="amount">
            <mat-header-cell *matHeaderCellDef>{{ 'invoices.column.amount' | hhTranslate:'Số tiền' }}</mat-header-cell>
            <mat-cell *matCellDef="let p">{{ p.amount | number:'1.0-0' }} đ</mat-cell>
          </ng-container>

          <ng-container matColumnDef="paymentDate">
            <mat-header-cell *matHeaderCellDef>{{ 'invoices.column.date' | hhTranslate:'Ngày' }}</mat-header-cell>
            <mat-cell *matCellDef="let p">{{ p.paymentDate | date:'medium' }}</mat-cell>
          </ng-container>

          <ng-container matColumnDef="methodName">
            <mat-header-cell *matHeaderCellDef>{{ 'invoices.column.method' | hhTranslate:'Phương thức' }}</mat-header-cell>
            <mat-cell *matCellDef="let p">{{ p.methodName }}</mat-cell>
          </ng-container>

          <ng-container matColumnDef="referenceNumber">
            <mat-header-cell *matHeaderCellDef>{{ 'invoices.column.reference' | hhTranslate:'Mã tham chiếu' }}</mat-header-cell>
            <mat-cell *matCellDef="let p">{{ p.referenceNumber || '-' }}</mat-cell>
          </ng-container>

          <mat-header-row *matHeaderRowDef="paymentColumns"></mat-header-row>
          <mat-row *matRowDef="let row; columns: paymentColumns;"></mat-row>
        </mat-table>
        }
        @if (invoice.payments.length === 0) {
        <p class="empty">{{ 'invoices.empty.payments' | hhTranslate:'Chưa có thanh toán nào.' }}</p>
        }
      </hh-page-section>

      <!-- Payment Form -->
      @if (showPaymentForm) {
      <hh-page-section [title]="'invoices.section.recordPayment' | hhTranslate:'Ghi nhận thanh toán'">
        <form [formGroup]="paymentForm" (ngSubmit)="recordPayment()" class="payment-form">
          <div class="form-row">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'invoices.field.amount' | hhTranslate:'Số tiền' }}</mat-label>
              <input matInput formControlName="amount" type="number" required min="1"
                     [attr.max]="invoice.balanceDue" [attr.aria-label]="'invoices.field.amountLabel' | hhTranslate:'Số tiền thanh toán'">
              <span matSuffix>đ</span>
              @if (paymentForm.get('amount')?.hasError('required')) {
              <mat-error>{{ 'invoices.validation.amountRequired' | hhTranslate:'Vui lòng nhập số tiền' }}</mat-error>
              }
              @if (paymentForm.get('amount')?.hasError('min')) {
              <mat-error>{{ 'invoices.validation.amountMin' | hhTranslate:'Số tiền phải lớn hơn 0' }}</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'invoices.field.paymentDate' | hhTranslate:'Ngày thanh toán' }}</mat-label>
              <input matInput [matDatepicker]="payPicker" formControlName="paymentDate" required
                     [attr.aria-label]="'invoices.field.paymentDateLabel' | hhTranslate:'Ngày thanh toán'">
              <mat-datepicker-toggle matSuffix [for]="payPicker"></mat-datepicker-toggle>
              <mat-datepicker #payPicker></mat-datepicker>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'invoices.field.method' | hhTranslate:'Phương thức' }}</mat-label>
              <mat-select formControlName="methodCode" required [attr.aria-label]="'invoices.field.methodLabel' | hhTranslate:'Phương thức thanh toán'">
                <mat-option value="cash">{{ 'invoices.method.cash' | hhTranslate:'Tiền mặt' }}</mat-option>
                <mat-option value="credit_card">{{ 'invoices.method.creditCard' | hhTranslate:'Thẻ tín dụng' }}</mat-option>
                <mat-option value="debit_card">{{ 'invoices.method.debitCard' | hhTranslate:'Thẻ ghi nợ' }}</mat-option>
                <mat-option value="bank_transfer">{{ 'invoices.method.bankTransfer' | hhTranslate:'Chuyển khoản' }}</mat-option>
                <mat-option value="insurance">{{ 'invoices.method.insurance' | hhTranslate:'Bảo hiểm' }}</mat-option>
                <mat-option value="mobile_payment">{{ 'invoices.method.mobilePayment' | hhTranslate:'Ví điện tử' }}</mat-option>
                <mat-option value="other">{{ 'invoices.method.other' | hhTranslate:'Khác' }}</mat-option>
              </mat-select>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>{{ 'invoices.field.reference' | hhTranslate:'Mã tham chiếu' }}</mat-label>
              <input matInput formControlName="referenceNumber" [attr.aria-label]="'invoices.field.reference' | hhTranslate:'Mã tham chiếu'">
            </mat-form-field>
          </div>

          <div class="form-actions">
            <button mat-button type="button" (click)="showPaymentForm = false">{{ 'common.cancel' | hhTranslate:'Hủy' }}</button>
            <button mat-raised-button color="primary" type="submit"
                    [disabled]="paymentForm.invalid || recordingPayment">
              @if (recordingPayment) {
              <mat-spinner diameter="18" class="btn-spinner"></mat-spinner>
              }
              {{ recordingPayment ? ('invoices.saving' | hhTranslate:'Đang lưu...') : ('invoices.confirmPayment' | hhTranslate:'Xác nhận thanh toán') }}
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

    .totals { display: flex; flex-direction: column; gap: 8px; max-width: 420px; }
    .total-row { display: flex; justify-content: space-between; padding: 4px 0; }
    .grand-total { font-weight: 700; font-size: 16px; border-top: 2px solid #e0e0e0; padding-top: 8px; margin-top: 4px; }
    .paid { color: #2e7d32; }
    .balance { font-weight: 500; }
    .text-danger { color: #c62828; }

    .items-table { width: 100%; border-collapse: collapse; }
    .items-table th, .items-table td { padding: 8px 12px; text-align: left; border-bottom: 1px solid #e0e0e0; }
    .items-table th { background: #f5f5f5; font-weight: 500; }
    .items-table .num { text-align: right; }

    .payment-form { display: flex; flex-direction: column; gap: 16px; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
    .empty { color: #999; font-style: italic; padding: 12px; }
  `],
})
export class InvoiceDetailComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  invoice?: Invoice;
  loadError = false;
  loading = true;
  errorMessage = '';
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
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.invoiceId = this.route.snapshot.params['id'];
    this.loadInvoice();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    this.router.navigate(['/billing']);
  }

  loadInvoice(): void {
    this.loadError = false;
    this.loading = true;
    this.billingService.getInvoice(this.invoiceId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (inv) => {
          this.invoice = inv;
          this.paymentForm.patchValue({ amount: inv.balanceDue, paymentDate: new Date().toISOString() });
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loadError = true;
          this.loading = false;
          this.errorMessage = this.i18n.t('invoices.loadFailed', 'Không thể tải thông tin hóa đơn. Vui lòng thử lại sau.');
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
          this.snackBar.open(this.i18n.t('invoices.paymentSuccess', 'Đã ghi nhận thanh toán thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.showPaymentForm = false;
          this.recordingPayment = false;
          this.paymentForm.reset({ methodCode: 'cash' });
          this.loadInvoice();
        },
        error: () => {
          this.recordingPayment = false;
          this.snackBar.open(this.i18n.t('invoices.paymentFailed', 'Không thể ghi nhận thanh toán'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
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
          this.snackBar.open(this.i18n.t('invoices.voidSuccess', 'Đã hủy hóa đơn'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadInvoice();
        },
        error: () => this.snackBar.open(this.i18n.t('invoices.voidFailed', 'Không thể hủy hóa đơn'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 }),
      });
  }
}
