import {
  Component,
  OnInit,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormBuilder,
  Validators,
  FormArray,
  FormControl,
  ReactiveFormsModule,
} from "@angular/forms";
import { Router, RouterModule } from "@angular/router";
import {
  Subject,
  debounceTime,
  distinctUntilChanged,
  takeUntil,
  filter,
} from "rxjs";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatIconModule } from "@angular/material/icon";
import { MatSelectModule } from "@angular/material/select";
import { MatButtonModule } from "@angular/material/button";
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatNativeDateModule } from "@angular/material/core";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { BillingService } from "@core/services/billing.service";
import { PatientService } from "@core/services/patient.service";
import { Patient } from "@core/models/patient.model";
import { CreateInvoiceRequest } from "@core/models/invoice.model";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import {
  HisHopePageLayoutComponent,
  HisHopePageHeaderComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

@Component({
  selector: "app-invoice-form",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatSelectModule,
    MatButtonModule,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    HisHopePageLayoutComponent,
    HisHopePageHeaderComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'invoiceForm.title' | hhTranslate: 'Tạo hóa đơn mới'"
        [subtitle]="
          'invoiceForm.subtitle'
            | hhTranslate: 'Lập hóa đơn thanh toán cho bệnh nhân'
        "
      />

      <form [formGroup]="invoiceForm" (ngSubmit)="onSubmit()" novalidate>
        <hh-form-layout>
          <hh-form-section
            [title]="
              'invoiceForm.section.basic' | hhTranslate: 'Thông tin hóa đơn'
            "
            [span]="2"
          >
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>{{
                  "invoiceForm.patient" | hhTranslate: "Bệnh nhân"
                }}</mat-label>
                <input
                  matInput
                  [formControl]="patientSearchControl"
                  [placeholder]="
                    'invoiceForm.patientPlaceholder'
                      | hhTranslate: 'Tìm bệnh nhân...'
                  "
                  [attr.aria-label]="
                    'invoiceForm.patientSearchAria'
                      | hhTranslate: 'Tìm kiếm bệnh nhân'
                  "
                  [matAutocomplete]="patientAuto"
                />
                <mat-icon matSuffix>search</mat-icon>
                <mat-autocomplete
                  #patientAuto="matAutocomplete"
                  [displayWith]="displayPatientFn"
                >
                  @for (p of filteredPatients; track p.id) {
                    <mat-option
                      [value]="p"
                      (onSelectionChange)="onPatientSelected(p)"
                    >
                      {{ p.fullName }} - {{ p.id | slice: 0 : 8 }}...
                    </mat-option>
                  }
                </mat-autocomplete>
                @if (invoiceForm.get("patientId")?.hasError("required")) {
                  <mat-error>{{
                    "invoiceForm.patientRequired"
                      | hhTranslate: "Vui lòng chọn bệnh nhân"
                  }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{
                  "invoiceForm.date" | hhTranslate: "Ngày hóa đơn"
                }}</mat-label>
                <input
                  matInput
                  [matDatepicker]="datePicker"
                  formControlName="invoiceDate"
                  required
                  [attr.aria-label]="
                    'invoiceForm.dateAria' | hhTranslate: 'Ngày hóa đơn'
                  "
                />
                <mat-datepicker-toggle
                  matSuffix
                  [for]="datePicker"
                ></mat-datepicker-toggle>
                <mat-datepicker #datePicker></mat-datepicker>
              </mat-form-field>

              <mat-form-field appearance="outline" class="full-width">
                <mat-label>{{
                  "invoiceForm.notes" | hhTranslate: "Ghi chú"
                }}</mat-label>
                <textarea
                  matInput
                  formControlName="notes"
                  rows="2"
                  [placeholder]="
                    'invoiceForm.notesPlaceholder'
                      | hhTranslate: 'Ghi chú hóa đơn...'
                  "
                  [attr.aria-label]="
                    'invoiceForm.notesAria' | hhTranslate: 'Ghi chú'
                  "
                ></textarea>
              </mat-form-field>
            </div>
          </hh-form-section>
        </hh-form-layout>

        <!-- Line Items -->
        <mat-card class="items-section">
          <mat-card-header>
            <mat-card-title>{{
              "invoiceForm.services" | hhTranslate: "Dịch vụ"
            }}</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div formArrayName="lineItems">
              @for (item of lineItems.controls; track i; let i = $index) {
                <div [formGroupName]="i" class="item-row">
                  <mat-form-field appearance="outline">
                    <mat-label>{{
                      "invoiceForm.itemCode" | hhTranslate: "Mã dịch vụ"
                    }}</mat-label>
                    <input
                      matInput
                      formControlName="itemCode"
                      required
                      [placeholder]="
                        'invoiceForm.itemCodePlaceholder'
                          | hhTranslate: 'VD: KCB-001'
                      "
                      [attr.aria-label]="
                        'invoiceForm.itemCodeAria' | hhTranslate: 'Mã dịch vụ'
                      "
                    />
                  </mat-form-field>

                  <mat-form-field appearance="outline" class="flex-2">
                    <mat-label>{{
                      "invoiceForm.description" | hhTranslate: "Mô tả"
                    }}</mat-label>
                    <input
                      matInput
                      formControlName="description"
                      required
                      [placeholder]="
                        'invoiceForm.descriptionPlaceholder'
                          | hhTranslate: 'Mô tả dịch vụ'
                      "
                      [attr.aria-label]="
                        'invoiceForm.descriptionAria'
                          | hhTranslate: 'Mô tả dịch vụ'
                      "
                    />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>{{
                      "invoiceForm.itemType" | hhTranslate: "Loại"
                    }}</mat-label>
                    <mat-select
                      formControlName="itemTypeCode"
                      required
                      [attr.aria-label]="
                        'invoiceForm.itemTypeAria' | hhTranslate: 'Loại dịch vụ'
                      "
                    >
                      <mat-option value="examination">{{
                        "invoiceForm.type.exam" | hhTranslate: "Khám bệnh"
                      }}</mat-option>
                      <mat-option value="procedure">{{
                        "invoiceForm.type.procedure" | hhTranslate: "Thủ thuật"
                      }}</mat-option>
                      <mat-option value="medication">{{
                        "invoiceForm.type.medication" | hhTranslate: "Thuốc"
                      }}</mat-option>
                      <mat-option value="lab">{{
                        "invoiceForm.type.lab" | hhTranslate: "Xét nghiệm"
                      }}</mat-option>
                      <mat-option value="imaging">{{
                        "invoiceForm.type.imaging"
                          | hhTranslate: "Chẩn đoán hình ảnh"
                      }}</mat-option>
                      <mat-option value="supply">{{
                        "invoiceForm.type.supply" | hhTranslate: "Vật tư"
                      }}</mat-option>
                      <mat-option value="room">{{
                        "invoiceForm.type.room" | hhTranslate: "Phòng"
                      }}</mat-option>
                      <mat-option value="other">{{
                        "invoiceForm.type.other" | hhTranslate: "Khác"
                      }}</mat-option>
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline" class="num-field">
                    <mat-label>{{
                      "invoiceForm.quantity" | hhTranslate: "SL"
                    }}</mat-label>
                    <input
                      matInput
                      formControlName="quantity"
                      type="number"
                      required
                      min="1"
                      [attr.aria-label]="
                        'invoiceForm.quantityAria' | hhTranslate: 'Số lượng'
                      "
                      (input)="updateItemAmount(i)"
                    />
                  </mat-form-field>

                  <mat-form-field appearance="outline" class="num-field">
                    <mat-label>{{
                      "invoiceForm.unitPrice" | hhTranslate: "Đơn giá"
                    }}</mat-label>
                    <input
                      matInput
                      formControlName="unitPrice"
                      type="number"
                      required
                      min="0"
                      [attr.aria-label]="
                        'invoiceForm.unitPriceAria' | hhTranslate: 'Đơn giá'
                      "
                      (input)="updateItemAmount(i)"
                    />
                  </mat-form-field>

                  <div class="item-amount">
                    <span>{{ getItemAmount(i) | number: "1.0-0" }} đ</span>
                  </div>

                  <button
                    mat-icon-button
                    color="warn"
                    type="button"
                    (click)="removeItem(i)"
                    [attr.aria-label]="
                      'invoiceForm.removeItem' | hhTranslate: 'Xóa dịch vụ'
                    "
                  >
                    <mat-icon>delete</mat-icon>
                  </button>
                </div>
              }
            </div>

            <button
              mat-stroked-button
              color="primary"
              type="button"
              (click)="addItem()"
              [attr.aria-label]="
                'invoiceForm.addItem' | hhTranslate: 'Thêm dịch vụ'
              "
            >
              <mat-icon>add</mat-icon>
              {{ "invoiceForm.addItem" | hhTranslate: "Thêm dịch vụ" }}
            </button>
          </mat-card-content>
        </mat-card>

        <!-- Totals Summary -->
        @if (lineItems.length > 0) {
          <mat-card class="totals-card">
            <mat-card-content>
              <div class="total-row">
                <span>{{
                  "invoiceForm.subtotal" | hhTranslate: "Tổng tạm tính:"
                }}</span
                ><span>{{ calculateSubTotal() | number: "1.0-0" }} đ</span>
              </div>
            </mat-card-content>
          </mat-card>
        }

        <div class="form-actions">
          <button mat-button type="button" routerLink="/billing">
            {{ "common.cancel" | hhTranslate: "Hủy" }}
          </button>
          <button
            mat-raised-button
            color="primary"
            type="submit"
            [disabled]="
              invoiceForm.invalid || lineItems.length === 0 || submitting
            "
          >
            @if (submitting) {
              <mat-spinner
                diameter="18"
                class="btn-spinner"
                [attr.aria-label]="'common.saving' | hhTranslate: 'Đang lưu'"
              ></mat-spinner>
            }
            {{
              submitting
                ? ("common.saving" | hhTranslate: "Đang lưu...")
                : ("invoiceForm.save" | hhTranslate: "Tạo hóa đơn")
            }}
          </button>
        </div>
      </form>
    </hh-page-layout>
  `,
  styles: [
    `
      .form-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--form-field-gap, 16px);
      }
      .form-grid .full-width {
        grid-column: 1 / -1;
      }
      @media (max-width: 768px) {
        .form-grid {
          grid-template-columns: 1fr;
        }
      }
      .items-section {
        margin: 20px 0;
      }
      .item-row {
        display: flex;
        gap: 8px;
        align-items: flex-start;
        margin-bottom: 12px;
        flex-wrap: wrap;
      }
      .item-row mat-form-field {
        flex: 1;
        min-width: 120px;
      }
      .flex-2 {
        flex: 2;
      }
      .num-field {
        max-width: 100px;
      }
      .item-amount {
        display: flex;
        align-items: center;
        min-width: 100px;
        font-weight: 500;
        color: var(--color-primary, #2f6b4a);
        padding-top: 18px;
      }
      .totals-card {
        margin-bottom: 20px;
      }
      .total-row {
        display: flex;
        justify-content: flex-end;
        gap: 24px;
        font-size: 18px;
        font-weight: 500;
      }
      .form-actions {
        margin-top: 24px;
        display: flex;
        gap: 12px;
        justify-content: flex-end;
      }
      .btn-spinner {
        display: inline-block;
        margin-right: 8px;
      }
    `,
  ],
})
export class InvoiceFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  submitting = false;
  filteredPatients: Patient[] = [];
  patientSearchControl = new FormControl("");

  invoiceForm = this.fb.group({
    patientId: ["", Validators.required],
    invoiceDate: [new Date().toISOString(), Validators.required],
    notes: [""],
    lineItems: this.fb.array([]),
  });

  get lineItems(): FormArray {
    return this.invoiceForm.get("lineItems") as FormArray;
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
    this.patientSearchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe((term) => {
        const searchTerm = term ?? "";
        if (searchTerm.length < 2) {
          this.filteredPatients = [];
          this.cdr.markForCheck();
          return;
        }
        this.patientService
          .search(searchTerm, 1, 20)
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
    return patient ? `${patient.fullName} (${patient.id.slice(0, 8)}...)` : "";
  }

  onPatientSelected(patient: Patient): void {
    this.invoiceForm.patchValue({ patientId: patient.id });
  }

  addItem(): void {
    this.lineItems.push(
      this.fb.group({
        itemCode: ["", Validators.required],
        description: ["", Validators.required],
        itemTypeCode: ["examination", Validators.required],
        quantity: [1, [Validators.required, Validators.min(1)]],
        unitPrice: [0, [Validators.required, Validators.min(0)]],
      }),
    );
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
    const qty = group.get("quantity")?.value || 0;
    const price = group.get("unitPrice")?.value || 0;
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
    const lineItems: CreateInvoiceRequest["lineItems"] = (
      formValue.lineItems as any[]
    ).map((item: any) => ({
      description: item.description,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      itemCode: item.itemCode,
      itemTypeCode: item.itemTypeCode,
    }));

    const request: CreateInvoiceRequest = {
      patientId: formValue.patientId ?? "",
      notes: formValue.notes ?? undefined,
      lineItems,
    };

    this.billingService
      .createInvoice(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (invoice) => {
          this.snackBar.open("Đã tạo hóa đơn thành công", "Đóng", {
            duration: 3000,
          });
          this.router.navigate(["/billing", invoice.id]);
        },
        error: () => {
          this.submitting = false;
          this.snackBar.open("Không thể tạo hóa đơn", "Đóng", {
            duration: 5000,
          });
          this.cdr.markForCheck();
        },
      });
  }
}
