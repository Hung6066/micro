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
import { ClinicalService } from "@core/services/clinical.service";
import { AuthService } from "@core/services/auth.service";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";

export interface StartEncounterData {
  patientId: string;
  patientName: string;
}

@Component({
  selector: "app-start-encounter-dialog",
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
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: "./start-encounter.dialog.html",
  styleUrls: ["./start-encounter.dialog.scss"],
})
export class StartEncounterDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  form: FormGroup;
  saving = false;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<StartEncounterDialogComponent>,
    private clinicalService: ClinicalService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: StartEncounterData,
  ) {
    this.form = this.fb.group({
      encounterType: ["consultation", Validators.required],
      chiefComplaint: [""],
      temperature: [null],
      heartRate: [null],
      respiratoryRate: [null],
      systolicBP: [null],
      diastolicBP: [null],
      oxygenSaturation: [null],
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

        this.clinicalService
          .start({
            patientId: this.data.patientId,
            providerId,
            encounterTypeCode: this.form.value.encounterType,
          })
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (encounter) => {
              // Record vitals if provided
              const vitals: any = {};
              if (this.form.value.temperature)
                vitals.temperature = this.form.value.temperature;
              if (this.form.value.heartRate)
                vitals.heartRate = this.form.value.heartRate;
              if (this.form.value.respiratoryRate)
                vitals.respiratoryRate = this.form.value.respiratoryRate;
              if (this.form.value.systolicBP)
                vitals.systolicBP = this.form.value.systolicBP;
              if (this.form.value.diastolicBP)
                vitals.diastolicBP = this.form.value.diastolicBP;
              if (this.form.value.oxygenSaturation)
                vitals.oxygenSaturation = this.form.value.oxygenSaturation;

              if (Object.keys(vitals).length > 0) {
                this.clinicalService
                  .recordVitals(encounter.id, vitals)
                  .pipe(takeUntil(this.destroy$))
                  .subscribe();
              }

              this.snackBar.open(
                this.i18n.t(
                  "startEncounter.started",
                  "Đã bắt đầu lượt khám mới",
                ),
                this.i18n.t("common.close", "Đóng"),
                { duration: 3000 },
              );
              this.dialogRef.close(encounter);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open(
                this.i18n.t(
                  "startEncounter.startFailed",
                  "Không thể tạo lượt khám",
                ),
                this.i18n.t("common.close", "Đóng"),
                { duration: 5000 },
              );
              this.cdr.markForCheck();
            },
          });
      });
  }
}
