import {
  Component,
  Inject,
  OnDestroy,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  inject,
} from "@angular/core";
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule,
} from "@angular/forms";
import {
  MatDialogRef,
  MAT_DIALOG_DATA,
  MatDialogModule,
} from "@angular/material/dialog";
import { MatButtonModule } from "@angular/material/button";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatSelectModule } from "@angular/material/select";
import { MatDatepickerModule } from "@angular/material/datepicker";
import { MatNativeDateModule } from "@angular/material/core";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { CommonModule } from "@angular/common";
import { Subject, takeUntil } from "rxjs";
import { BillingService } from "@core/services/billing.service";
import { Invoice } from "@core/models/invoice.model";
import {
  HisHopeTranslatePipe,
  HisHopeI18nService,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

export interface RecordPaymentData {
  patientId: string;
  patientName: string;
  invoices: Invoice[];
}

@Component({
  selector: "app-record-payment-dialog",
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
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./record-payment.dialog.html",
  styleUrls: ["./record-payment.dialog.scss"],
})
export class RecordPaymentDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  form: FormGroup;
  saving = false;

  get payableInvoices(): Invoice[] {
    return this.data.invoices.filter((i) => i.balanceDue > 0);
  }

  get selectedInvoice(): Invoice | undefined {
    const id = this.form.get("invoiceId")?.value;
    return this.data.invoices.find((i) => i.id === id);
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
      invoiceId: ["", Validators.required],
      amount: ["", [Validators.required, Validators.min(1)]],
      methodCode: ["cash", Validators.required],
      referenceNumber: [""],
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

    this.billingService
      .recordPayment(this.form.value.invoiceId, {
        amount: this.form.value.amount,
        paymentDate: new Date().toISOString(),
        methodCode: this.form.value.methodCode,
        referenceNumber: this.form.value.referenceNumber || undefined,
      })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(
            this.i18n.t("recordPayment.saveSuccess", "Đã ghi nhận thanh toán"),
            this.i18n.t("common.close", "Đóng"),
            { duration: 3000 },
          );
          this.dialogRef.close(true);
        },
        error: () => {
          this.saving = false;
          this.snackBar.open(
            this.i18n.t(
              "recordPayment.saveFailed",
              "Không thể ghi nhận thanh toán",
            ),
            this.i18n.t("common.close", "Đóng"),
            { duration: 5000 },
          );
          this.cdr.markForCheck();
        },
      });
  }
}
