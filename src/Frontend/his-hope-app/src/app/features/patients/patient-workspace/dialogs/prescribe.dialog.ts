import {
  Component,
  Inject,
  OnDestroy,
  OnInit,
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
import { MatAutocompleteModule } from "@angular/material/autocomplete";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { CommonModule } from "@angular/common";
import {
  Subject,
  of,
  takeUntil,
  debounceTime,
  distinctUntilChanged,
  switchMap,
} from "rxjs";
import { PharmacyService } from "@core/services/pharmacy.service";
import { AuthService } from "@core/services/auth.service";
import { Medication } from "@core/models/medication.model";
import {
  HisHopeTranslatePipe,
  HisHopeI18nService,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeCreateDialogShellComponent,
  HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent,
} from "@his-hope/frontend-foundation/ui";

export interface PrescribeData {
  patientId: string;
  patientName: string;
}

@Component({
  selector: "app-prescribe-dialog",
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatAutocompleteModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    HisHopeCreateDialogShellComponent,
    HisHopeFormLayoutComponent,
    HisHopeFormSectionComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./prescribe.dialog.html",
  styleUrls: ["./prescribe.dialog.scss"],
})
export class PrescribeDialogComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  form: FormGroup;
  saving = false;
  filteredMedications: Medication[] = [];
  selectedMedication: Medication | null = null;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<PrescribeDialogComponent>,
    private pharmacyService: PharmacyService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: PrescribeData,
  ) {
    this.form = this.fb.group({
      medicationSearch: [""],
      dosageInstructions: ["", Validators.required],
      route: ["oral", Validators.required],
      quantity: [30, [Validators.required, Validators.min(1)]],
      refills: [0, [Validators.min(0)]],
    });
  }

  ngOnInit(): void {
    this.form
      .get("medicationSearch")
      ?.valueChanges.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$),
      )
      .subscribe((val) => {
        if (typeof val !== "string") return;
        this.selectedMedication = null;
        if (val.length < 1) {
          this.filteredMedications = [];
          this.cdr.markForCheck();
          return;
        }
        this.pharmacyService
          .searchMedications({ searchTerm: val, pageSize: 20 })
          .pipe(takeUntil(this.destroy$))
          .subscribe((res) => {
            this.filteredMedications = res.items;
            this.cdr.markForCheck();
          });
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayMedication(med: Medication): string {
    return med ? `${med.name} (${med.strength})` : "";
  }

  onMedicationSelected(event: any): void {
    this.selectedMedication = event.option.value;
    this.form.patchValue({ medicationSearch: this.selectedMedication?.name });
  }

  save(): void {
    if (this.form.invalid || this.saving || !this.selectedMedication) return;
    this.saving = true;
    this.cdr.markForCheck();

    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        const providerId = user?.id ?? "usr-002";

        this.pharmacyService
          .createPrescription({
            patientId: this.data.patientId,
            providerId,
            medicationId: this.selectedMedication!.id,
            dosageInstructions: this.form.value.dosageInstructions,
            route: this.form.value.route,
            quantity: this.form.value.quantity,
            refills: this.form.value.refills,
          })
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open(
                this.i18n.t(
                  "prescribe.saveSuccess",
                  "Đã kê đơn thuốc thành công",
                ),
                this.i18n.t("common.close", "Đóng"),
                { duration: 3000 },
              );
              this.dialogRef.close(true);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open(
                this.i18n.t("prescribe.saveFailed", "Không thể kê đơn thuốc"),
                this.i18n.t("common.close", "Đóng"),
                { duration: 5000 },
              );
              this.cdr.markForCheck();
            },
          });
      });
  }
}
