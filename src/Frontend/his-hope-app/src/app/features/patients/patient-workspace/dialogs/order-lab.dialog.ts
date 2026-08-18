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
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { CommonModule } from "@angular/common";
import { Subject, takeUntil } from "rxjs";
import { LabService } from "@core/services/lab.service";
import { AuthService } from "@core/services/auth.service";
import {
  HisHopeTranslatePipe,
  HisHopeI18nService,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

export interface OrderLabData {
  patientId: string;
  patientName: string;
}

const AVAILABLE_TESTS = [
  { code: "CBC", name: "Công thức máu", specimen: "blood" },
  { code: "BMP", name: "Điện giải đồ", specimen: "blood" },
  { code: "CRP", name: "C-Reactive Protein", specimen: "blood" },
  { code: "HbA1c", name: "HbA1c (Đường huyết trung bình)", specimen: "blood" },
  { code: "UA", name: "Tổng phân tích nước tiểu", specimen: "urine" },
  { code: "BNP", name: "BNP (Peptide lợi niệu)", specimen: "blood" },
  { code: "LIPID", name: "Mỡ máu toàn phần", specimen: "blood" },
  { code: "Cr", name: "Creatinin máu", specimen: "blood" },
  { code: "IgE", name: "IgE toàn phần", specimen: "blood" },
  { code: "LFT", name: "Chức năng gan", specimen: "blood" },
];

@Component({
  selector: "app-order-lab-dialog",
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./order-lab.dialog.html",
  styleUrls: ["./order-lab.dialog.scss"],
})
export class OrderLabDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  form: FormGroup;
  saving = false;
  availableTests = AVAILABLE_TESTS;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<OrderLabDialogComponent>,
    private labService: LabService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: OrderLabData,
  ) {
    this.form = this.fb.group({
      testCode: ["", Validators.required],
      priority: ["routine", Validators.required],
      notes: [""],
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

    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        const providerId = user?.id ?? "usr-002";
        const selected = AVAILABLE_TESTS.find(
          (t) => t.code === this.form.value.testCode,
        );

        this.labService
          .createLabOrder({
            patientId: this.data.patientId,
            providerId,
            priorityCode: this.form.value.priority,
            notes: this.form.value.notes,
            tests: [
              {
                testCode: selected!.code,
                testName: selected!.name,
                specimenType: selected!.specimen,
              },
            ],
          })
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open(
                this.i18n.t(
                  "orderLab.saveSuccess",
                  "Đã gửi chỉ định xét nghiệm",
                ),
                this.i18n.t("common.close", "Đóng"),
                { duration: 3000 },
              );
              this.dialogRef.close(true);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open(
                this.i18n.t("orderLab.saveFailed", "Không thể gửi chỉ định"),
                this.i18n.t("common.close", "Đóng"),
                { duration: 5000 },
              );
              this.cdr.markForCheck();
            },
          });
      });
  }
}
