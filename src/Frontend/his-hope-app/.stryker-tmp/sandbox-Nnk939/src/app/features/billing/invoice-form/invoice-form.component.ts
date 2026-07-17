// @ts-nocheck
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, Validators, FormArray, FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil, filter } from 'rxjs';
import { BillingService } from '@core/services/billing.service';
import { PatientService } from '@core/services/patient.service';
import { Patient } from '@core/models/patient.model';
import { CreateInvoiceRequest } from '@core/models/invoice.model';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-invoice-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="invoice-form">
      <h1>Tạo hóa đơn mới</h1>

      <form [formGroup]="invoiceForm" (ngSubmit)="onSubmit()">
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Bệnh nhân</mat-label>
            <input matInput [formControl]="patientSearchControl" placeholder="Tìm bệnh nhân..."
                   aria-label="Tìm kiếm bệnh nhân" [matAutocomplete]="patientAuto">
            <mat-icon matSuffix>search</mat-icon>
            <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientFn">
              <mat-option *ngFor="let p of filteredPatients" [value]="p" (onSelectionChange)="onPatientSelected(p)">
                {{ p.fullName }} - {{ p.id | slice:0:8 }}...
              </mat-option>
            </mat-autocomplete>
            <mat-error *ngIf="invoiceForm.get('patientId')?.hasError('required')">Vui lòng chọn bệnh nhân</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Ngày hóa đơn</mat-label>
            <input matInput [matDatepicker]="datePicker" formControlName="invoiceDate" required
                   aria-label="Ngày hóa đơn">
            <mat-datepicker-toggle matSuffix [for]="datePicker"></mat-datepicker-toggle>
            <mat-datepicker #datePicker></mat-datepicker>
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Ghi chú</mat-label>
            <textarea matInput formControlName="notes" rows="2" placeholder="Ghi chú hóa đơn..."
                      aria-label="Ghi chú"></textarea>
          </mat-form-field>
        </div>

        <!-- Line Items -->
        <mat-card class="items-section">
          <mat-card-header>
            <mat-card-title>Dịch vụ</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div formArrayName="lineItems">
              <div *ngFor="let item of lineItems.controls; let i = index" [formGroupName]="i" class="item-row">
                <mat-form-field appearance="outline">
                  <mat-label>Mã dịch vụ</mat-label>
                  <input matInput formControlName="itemCode" required placeholder="VD: KCB-001"
                         aria-label="Mã dịch vụ">
                </mat-form-field>

                <mat-form-field appearance="outline" class="flex-2">
                  <mat-label>Mô tả</mat-label>
                  <input matInput formControlName="description" required placeholder="Mô tả dịch vụ"
                         aria-label="Mô tả dịch vụ">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Loại</mat-label>
                  <mat-select formControlName="itemTypeCode" required aria-label="Loại dịch vụ">
                    <mat-option value="examination">Khám bệnh</mat-option>
                    <mat-option value="procedure">Thủ thuật</mat-option>
                    <mat-option value="medication">Thuốc</mat-option>
                    <mat-option value="lab">Xét nghiệm</mat-option>
                    <mat-option value="imaging">Chẩn đoán hình ảnh</mat-option>
                    <mat-option value="supply">Vật tư</mat-option>
                    <mat-option value="room">Phòng</mat-option>
                    <mat-option value="other">Khác</mat-option>
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline" class="num-field">
                  <mat-label>SL</mat-label>
                  <input matInput formControlName="quantity" type="number" required min="1"
                         aria-label="Số lượng" (input)="updateItemAmount(i)">
                </mat-form-field>

                <mat-form-field appearance="outline" class="num-field">
                  <mat-label>Đơn giá</mat-label>
                  <input matInput formControlName="unitPrice" type="number" required min="0"
                         aria-label="Đơn giá" (input)="updateItemAmount(i)">
                </mat-form-field>

                <div class="item-amount">
                  <span>{{ getItemAmount(i) | number:'1.0-0' }} đ</span>
                </div>

                <button mat-icon-button color="warn" type="button" (click)="removeItem(i)"
                        attr.aria-label="Xóa dịch vụ">
                  <mat-icon>delete</mat-icon>
                </button>
              </div>
            </div>

            <button mat-stroked-button color="primary" type="button" (click)="addItem()"
                    attr.aria-label="Thêm dịch vụ">
              <mat-icon>add</mat-icon> Thêm dịch vụ
            </button>
          </mat-card-content>
        </mat-card>

        <!-- Totals Summary -->
        <mat-card class="totals-card" *ngIf="lineItems.length > 0">
          <mat-card-content>
            <div class="total-row"><span>Tổng tạm tính:</span><span>{{ calculateSubTotal() | number:'1.0-0' }} đ</span></div>
          </mat-card-content>
        </mat-card>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/billing">Hủy</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="invoiceForm.invalid || lineItems.length === 0 || submitting">
            <mat-spinner diameter="18" *ngIf="submitting" class="btn-spinner" aria-label="Đang lưu"></mat-spinner>
            {{ submitting ? 'Đang lưu...' : 'Tạo hóa đơn' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .invoice-form { padding: 24px; max-width: 1000px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .items-section { margin: 20px 0; }
    .item-row { display: flex; gap: 8px; align-items: flex-start; margin-bottom: 12px; flex-wrap: wrap; }
    .item-row mat-form-field { flex: 1; min-width: 120px; }
    .flex-2 { flex: 2; }
    .num-field { max-width: 100px; }
    .item-amount { display: flex; align-items: center; min-width: 100px; font-weight: 500; color: var(--color-primary, #2F6B4A); padding-top: 18px; }
    .totals-card { margin-bottom: 20px; }
    .total-row { display: flex; justify-content: flex-end; gap: 24px; font-size: 18px; font-weight: 500; }
    .form-actions { margin-top: 24px; display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
  `],
})
export class InvoiceFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  submitting = false;
  filteredPatients: Patient[] = [];
  patientSearchControl = new FormControl('');

  invoiceForm = this.fb.group({
    patientId: ['', Validators.required],
    invoiceDate: [new Date().toISOString(), Validators.required],
    notes: [''],
    lineItems: this.fb.array([]),
  });

  get lineItems(): FormArray {
    return this.invoiceForm.get('lineItems') as FormArray;
  }

  constructor(
    private fb: FormBuilder,
    private billingService: BillingService,
    private patientService: PatientService,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.patientSearchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$),
    ).subscribe((term) => {
      const searchTerm = term ?? '';
      if (searchTerm.length < 2) {
        this.filteredPatients = [];
        this.cdr.markForCheck();
        return;
      }
      this.patientService.search(searchTerm, 1, 20)
        .pipe(takeUntil(this.destroy$))
        .subscribe((result) => {
          this.filteredPatients = result.items;
          this.cdr.markForCheck();
        });
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayPatientFn(patient: Patient): string {
    return patient ? `${patient.fullName} (${patient.id.slice(0, 8)}...)` : '';
  }

  onPatientSelected(patient: Patient): void {
    this.invoiceForm.patchValue({ patientId: patient.id });
  }

  addItem(): void {
    this.lineItems.push(this.fb.group({
      itemCode: ['', Validators.required],
      description: ['', Validators.required],
      itemTypeCode: ['examination', Validators.required],
      quantity: [1, [Validators.required, Validators.min(1)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
    }));
    this.cdr.markForCheck();
  }

  removeItem(index: number): void {
    this.lineItems.removeAt(index);
    this.cdr.markForCheck();
  }

  updateItemAmount(index: number): void {
    // Trigger change detection by updating the form value
    this.cdr.markForCheck();
  }

  getItemAmount(index: number): number {
    const group = this.lineItems.at(index);
    const qty = group.get('quantity')?.value || 0;
    const price = group.get('unitPrice')?.value || 0;
    return qty * price;
  }

  calculateSubTotal(): number {
    let total = 0;
    for (let i = 0; i < this.lineItems.length; i++) {
      total += this.getItemAmount(i);
    }
    return total;
  }

  onSubmit(): void {
    if (this.invoiceForm.invalid || this.lineItems.length === 0) return;

    this.submitting = true;

    // Build the line items
    const formValue = this.invoiceForm.value;
    const lineItems: CreateInvoiceRequest['lineItems'] = (formValue.lineItems as any[]).map((item: any) => ({
      description: item.description,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      itemCode: item.itemCode,
      itemTypeCode: item.itemTypeCode,
    }));

    const request: CreateInvoiceRequest = {
      patientId: formValue.patientId ?? '',
      notes: formValue.notes ?? undefined,
      lineItems,
    };

    this.billingService.createInvoice(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (invoice) => {
          this.snackBar.open('Đã tạo hóa đơn thành công', 'Đóng', { duration: 3000 });
          this.router.navigate(['/billing', invoice.id]);
        },
        error: () => {
          this.submitting = false;
          this.snackBar.open('Không thể tạo hóa đơn', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
