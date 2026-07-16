import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { ClinicalService } from '@core/services/clinical.service';
import { AuthService } from '@core/services/auth.service';

export interface StartEncounterData {
  patientId: string;
  patientName: string;
}

@Component({
  selector: 'app-start-encounter-dialog',
  standalone: true,
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
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Bắt đầu lượt khám mới</h2>
    <mat-dialog-content>
      <div class="patient-info" *ngIf="data.patientName">
        <mat-icon>person</mat-icon>
        <span>{{ data.patientName }}</span>
      </div>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Loại khám</mat-label>
          <mat-select formControlName="encounterType">
            <mat-option value="consultation">Khám bệnh</mat-option>
            <mat-option value="follow_up">Tái khám</mat-option>
            <mat-option value="emergency">Cấp cứu</mat-option>
            <mat-option value="procedure">Thủ thuật</mat-option>
          </mat-select>
          <mat-error *ngIf="form.get('encounterType')?.hasError('required')">Vui lòng chọn loại khám</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Lý do khám / Triệu chứng chính</mat-label>
          <textarea matInput formControlName="chiefComplaint" rows="3"
                    placeholder="Nhập lý do khám..."></textarea>
        </mat-form-field>

        <fieldset class="vitals-section">
          <legend>Dấu hiệu sinh tồn</legend>
          <div class="vitals-grid">
            <mat-form-field appearance="outline">
              <mat-label>Nhiệt độ (°C)</mat-label>
              <input matInput type="number" formControlName="temperature" step="0.1">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Mạch (lần/ph)</mat-label>
              <input matInput type="number" formControlName="heartRate">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Nhịp thở (lần/ph)</mat-label>
              <input matInput type="number" formControlName="respiratoryRate">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Huyết áp tâm thu</mat-label>
              <input matInput type="number" formControlName="systolicBP">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Huyết áp tâm trương</mat-label>
              <input matInput type="number" formControlName="diastolicBP">
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>SpO2 (%)</mat-label>
              <input matInput type="number" formControlName="oxygenSaturation" min="0" max="100">
            </mat-form-field>
          </div>
        </fieldset>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        <mat-icon>add</mat-icon>
        <span *ngIf="!saving">Bắt đầu khám</span>
        <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .patient-info { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; padding: 8px 12px; background: #e3f2fd; border-radius: 8px; color: #1565c0; font-weight: 500; }
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 420px; }
    .vitals-section { border: 1px solid #e0e0e0; border-radius: 8px; padding: 16px; margin: 0; }
    .vitals-section legend { font-weight: 500; color: #555; padding: 0 8px; }
    .vitals-grid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 12px; }
    mat-dialog-actions mat-spinner { display: inline-block; }
  `],
})
export class StartEncounterDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

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
      encounterType: ['consultation', Validators.required],
      chiefComplaint: [''],
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
        const providerId = user?.id ?? 'usr-002';

        this.clinicalService.start({
          patientId: this.data.patientId,
          providerId,
          encounterTypeCode: this.form.value.encounterType,
        }).pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (encounter) => {
              // Record vitals if provided
              const vitals: any = {};
              if (this.form.value.temperature) vitals.temperature = this.form.value.temperature;
              if (this.form.value.heartRate) vitals.heartRate = this.form.value.heartRate;
              if (this.form.value.respiratoryRate) vitals.respiratoryRate = this.form.value.respiratoryRate;
              if (this.form.value.systolicBP) vitals.systolicBP = this.form.value.systolicBP;
              if (this.form.value.diastolicBP) vitals.diastolicBP = this.form.value.diastolicBP;
              if (this.form.value.oxygenSaturation) vitals.oxygenSaturation = this.form.value.oxygenSaturation;

              if (Object.keys(vitals).length > 0) {
                this.clinicalService.recordVitals(encounter.id, vitals).pipe(takeUntil(this.destroy$)).subscribe();
              }

              this.snackBar.open('Đã bắt đầu lượt khám mới', 'Đóng', { duration: 3000 });
              this.dialogRef.close(encounter);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open('Không thể tạo lượt khám', 'Đóng', { duration: 5000 });
              this.cdr.markForCheck();
            },
          });
      });
  }
}
