// @ts-nocheck
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
import { AppointmentService } from '@core/services/appointment.service';
import { AuthService } from '@core/services/auth.service';

export interface ScheduleData {
  patientId: string;
  patientName: string;
}

const TIME_SLOTS = [
  '07:00', '07:30', '08:00', '08:30', '09:00', '09:30',
  '10:00', '10:30', '11:00', '11:30', '13:00', '13:30',
  '14:00', '14:30', '15:00', '15:30', '16:00', '16:30',
];

@Component({
  selector: 'app-schedule-dialog',
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
    <h2 mat-dialog-title>Đặt lịch hẹn</h2>
    <mat-dialog-content>
      <div class="patient-info" *ngIf="data.patientName">
        <mat-icon>person</mat-icon>
        <span>{{ data.patientName }}</span>
      </div>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Ngày hẹn</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="scheduledDate" required>
          <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
          <mat-error *ngIf="form.get('scheduledDate')?.hasError('required')">Vui lòng chọn ngày</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Giờ hẹn</mat-label>
          <mat-select formControlName="startTime" required>
            <mat-option *ngFor="let slot of timeSlots" [value]="slot">{{ slot }}</mat-option>
          </mat-select>
          <mat-error *ngIf="form.get('startTime')?.hasError('required')">Vui lòng chọn giờ</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Loại hẹn</mat-label>
          <mat-select formControlName="typeCode" required>
            <mat-option value="consultation">Khám bệnh</mat-option>
            <mat-option value="follow_up">Tái khám</mat-option>
            <mat-option value="emergency">Cấp cứu</mat-option>
            <mat-option value="procedure">Thủ thuật</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Lý do hẹn</mat-label>
          <textarea matInput formControlName="reason" rows="3"
                    placeholder="Lý do đến khám..."></textarea>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Phòng / Địa điểm</mat-label>
          <input matInput formControlName="location" placeholder="VD: Phòng khám số 2 - Tầng 1">
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        <mat-icon>calendar_today</mat-icon>
        <span *ngIf="!saving">Đặt lịch</span>
        <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .patient-info { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; padding: 8px 12px; background: #fff3e0; border-radius: 8px; color: #e65100; font-weight: 500; }
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 380px; }
  `],
})
export class ScheduleDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  form: FormGroup;
  saving = false;
  timeSlots = TIME_SLOTS;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<ScheduleDialogComponent>,
    private appointmentService: AppointmentService,
    private authService: AuthService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: ScheduleData,
  ) {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);

    this.form = this.fb.group({
      scheduledDate: [tomorrow, Validators.required],
      startTime: ['', Validators.required],
      typeCode: ['consultation', Validators.required],
      reason: [''],
      location: [''],
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
        const date = this.form.value.scheduledDate;
        const dateStr = date instanceof Date
          ? date.toISOString().slice(0, 10)
          : date;

        this.appointmentService.schedule({
          patientId: this.data.patientId,
          providerId,
          scheduledDate: dateStr,
          startTime: this.form.value.startTime,
          durationMinutes: 30,
          typeCode: this.form.value.typeCode,
          reason: this.form.value.reason,
          location: this.form.value.location,
        }).pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open('Đã đặt lịch hẹn thành công', 'Đóng', { duration: 3000 });
              this.dialogRef.close(true);
            },
            error: () => {
              this.saving = false;
              this.snackBar.open('Không thể đặt lịch hẹn', 'Đóng', { duration: 5000 });
              this.cdr.markForCheck();
            },
          });
      });
  }
}
