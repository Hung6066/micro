// @ts-nocheck
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { PatientService } from '@core/services/patient.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-patient-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="patient-form">
      <h1>{{ isEdit ? 'Chỉnh sửa bệnh nhân' : 'Thêm bệnh nhân mới' }}</h1>

      <form [formGroup]="patientForm" (ngSubmit)="onSubmit()">
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Họ</mat-label>
            <input matInput formControlName="lastName" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Tên đệm</mat-label>
            <input matInput formControlName="middleName">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Tên</mat-label>
            <input matInput formControlName="firstName" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Ngày sinh</mat-label>
            <input matInput [matDatepicker]="picker" formControlName="dateOfBirth" required>
            <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
            <mat-datepicker #picker></mat-datepicker>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Giới tính</mat-label>
            <mat-select formControlName="genderCode" required>
              <mat-option value="M">Nam</mat-option>
              <mat-option value="F">Nữ</mat-option>
              <mat-option value="O">Khác</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Số điện thoại</mat-label>
            <input matInput formControlName="phone" required placeholder="+84...">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput formControlName="email" type="email">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>CMND/CCCD</mat-label>
            <input matInput formControlName="nationalId">
          </mat-form-field>

          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Địa chỉ</mat-label>
            <input matInput formControlName="street" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Quận/Huyện</mat-label>
            <input matInput formControlName="district">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Thành phố</mat-label>
            <input matInput formControlName="city" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Tỉnh</mat-label>
            <input matInput formControlName="province" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Quốc gia</mat-label>
            <input matInput formControlName="country" required>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Mã BHYT</mat-label>
            <input matInput formControlName="insuranceId">
          </mat-form-field>
        </div>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/patients">Hủy</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="patientForm.invalid || submitting">
            <mat-spinner diameter="18" *ngIf="submitting" class="btn-spinner" aria-label="Đang lưu"></mat-spinner>
            {{ submitting ? 'Đang lưu...' : 'Lưu bệnh nhân' }}
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
export class PatientFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

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
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.patientId = this.route.snapshot.params['id'];
    if (this.patientId) {
      this.isEdit = true;
      this.patientService.getById(this.patientId)
        .pipe(takeUntil(this.destroy$))
        .subscribe((patient) => {
          this.patientForm.patchValue(patient as any);
          this.cdr.markForCheck();
        });
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSubmit(): void {
    if (this.patientForm.invalid) return;

    this.submitting = true;
    const request = this.patientForm.value as any;

    const action = this.isEdit
      ? this.patientService.update(this.patientId!, request)
      : this.patientService.create(request);

    action.pipe(takeUntil(this.destroy$)).subscribe({
      next: (patient) => {
        this.snackBar.open(`Patient ${this.isEdit ? 'updated' : 'created'} successfully`, 'Close', { duration: 3000 });
        this.router.navigate(['/patients', patient.id]);
      },
      error: () => {
        this.submitting = false;
        this.snackBar.open('Failed to save patient', 'Close', { duration: 5000 });
        this.cdr.markForCheck();
      },
    });
  }
}
