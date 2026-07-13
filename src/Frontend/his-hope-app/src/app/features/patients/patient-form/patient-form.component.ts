import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PatientService } from '@core/services/patient.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-patient-form',
  template: `
    <div class="patient-form">
      <h1>{{ isEdit ? 'Edit Patient' : 'New Patient' }}</h1>

      <form [formGroup]="patientForm" (ngSubmit)="onSubmit()">
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>First Name</mat-label>
            <input matInput formControlName="firstName" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Last Name</mat-label>
            <input matInput formControlName="lastName" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Middle Name</mat-label>
            <input matInput formControlName="middleName">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Date of Birth</mat-label>
            <input matInput [matDatepicker]="picker" formControlName="dateOfBirth" required>
            <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
            <mat-datepicker #picker></mat-datepicker>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Gender</mat-label>
            <mat-select formControlName="genderCode" required>
              <mat-option value="M">Male</mat-option>
              <mat-option value="F">Female</mat-option>
              <mat-option value="O">Other</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Phone</mat-label>
            <input matInput formControlName="phone" required placeholder="+84...">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput formControlName="email" type="email">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>National ID</mat-label>
            <input matInput formControlName="nationalId">
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Street Address</mat-label>
            <input matInput formControlName="street" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>District</mat-label>
            <input matInput formControlName="district">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>City</mat-label>
            <input matInput formControlName="city" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Province</mat-label>
            <input matInput formControlName="province" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Country</mat-label>
            <input matInput formControlName="country" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Insurance ID</mat-label>
            <input matInput formControlName="insuranceId">
          </mat-form-field>
        </div>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/patients">Cancel</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="patientForm.invalid || submitting">
            {{ submitting ? 'Saving...' : 'Save Patient' }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .patient-form { padding: 24px; max-width: 900px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .full-width { grid-column: 1 / -1; }
    .form-actions { margin-top: 24px; display: flex; gap: 12px; justify-content: flex-end; }
  `],
})
export class PatientFormComponent implements OnInit {
  isEdit = false;
  patientId?: string;
  submitting = false;

  patientForm = this.fb.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    middleName: [''],
    dateOfBirth: ['', Validators.required],
    genderCode: ['', Validators.required],
    phone: ['', Validators.required],
    email: [''],
    nationalId: [''],
    street: ['', Validators.required],
    district: [''],
    city: ['', Validators.required],
    province: ['', Validators.required],
    postalCode: [''],
    country: ['Vietnam', Validators.required],
    insuranceId: [''],
  });

  constructor(
    private fb: FormBuilder,
    private patientService: PatientService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.patientId = this.route.snapshot.params['id'];
    if (this.patientId) {
      this.isEdit = true;
      this.patientService.getById(this.patientId).subscribe((patient) => {
        this.patientForm.patchValue(patient as any);
      });
    }
  }

  onSubmit(): void {
    if (this.patientForm.invalid) return;

    this.submitting = true;
    const request = this.patientForm.value as any;

    const action = this.isEdit
      ? this.patientService.update(this.patientId!, request)
      : this.patientService.create(request);

    action.subscribe({
      next: (patient) => {
        this.snackBar.open(`Patient ${this.isEdit ? 'updated' : 'created'} successfully`, 'Close', { duration: 3000 });
        this.router.navigate(['/patients', patient.id]);
      },
      error: () => {
        this.submitting = false;
        this.snackBar.open('Failed to save patient', 'Close', { duration: 5000 });
      },
    });
  }
}
