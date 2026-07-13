import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AppointmentService } from '@core/services/appointment.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-appointment-form',
  template: `
    <div class="appointment-form">
      <h1>Schedule Appointment</h1>
      <form [formGroup]="appointmentForm" (ngSubmit)="onSubmit()" class="form">
        <mat-form-field appearance="outline">
          <mat-label>Patient ID</mat-label>
          <input matInput formControlName="patientId" required>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="scheduledDate" required>
          <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Start Time</mat-label>
          <input matInput type="time" formControlName="startTime" required>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Duration (minutes)</mat-label>
          <input matInput type="number" formControlName="durationMinutes" value="30" required>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Type</mat-label>
          <mat-select formControlName="typeCode" required>
            <mat-option value="CHECKUP">General Checkup</mat-option>
            <mat-option value="CONSULT">Consultation</mat-option>
            <mat-option value="FOLLOWUP">Follow-up</mat-option>
            <mat-option value="PROCED">Procedure</mat-option>
            <mat-option value="VACCINE">Vaccination</mat-option>
            <mat-option value="TELE">Telehealth</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Reason</mat-label>
          <textarea matInput formControlName="reason" rows="2"></textarea>
        </mat-form-field>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/appointments">Cancel</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="appointmentForm.invalid || submitting">
            {{ submitting ? 'Scheduling...' : 'Schedule' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .appointment-form { padding: 24px; max-width: 600px; }
    .form { display: flex; flex-direction: column; gap: 16px; }
    .full-width { width: 100%; }
    .form-actions { display: flex; gap: 12px; justify-content: flex-end; margin-top: 16px; }
  `],
})
export class AppointmentFormComponent {
  submitting = false;
  appointmentForm = this.fb.group({
    patientId: ['', Validators.required],
    scheduledDate: ['', Validators.required],
    startTime: ['', Validators.required],
    durationMinutes: [30, Validators.required],
    typeCode: ['CHECKUP', Validators.required],
    reason: [''],
    location: [''],
  });

  constructor(
    private fb: FormBuilder,
    private appointmentService: AppointmentService,
    private router: Router,
    private snackBar: MatSnackBar,
  ) {}

  onSubmit(): void {
    if (this.appointmentForm.invalid) return;
    this.submitting = true;

    this.appointmentService.schedule(this.appointmentForm.value as any).subscribe({
      next: () => {
        this.snackBar.open('Appointment scheduled', 'Close', { duration: 3000 });
        this.router.navigate(['/appointments']);
      },
      error: () => {
        this.submitting = false;
        this.snackBar.open('Failed to schedule appointment', 'Close', { duration: 5000 });
      },
    });
  }
}
