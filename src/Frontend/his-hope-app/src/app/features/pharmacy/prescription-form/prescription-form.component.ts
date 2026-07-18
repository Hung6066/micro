import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, Validators, FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PharmacyService } from '@core/services/pharmacy.service';
import { PatientService } from '@core/services/patient.service';
import { Medication } from '@core/models/medication.model';
import { Patient } from '@core/models/patient.model';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
    selector: 'app-prescription-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatFormFieldModule, MatInputModule, MatIconModule, MatSelectModule,
        MatButtonModule, MatAutocompleteModule, MatProgressSpinnerModule,
        MatSnackBarModule,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <div class="prescription-form">
      <h1>Tạo đơn thuốc mới</h1>

      <form [formGroup]="prescriptionForm" (ngSubmit)="onSubmit()">
        <div class="form-grid">
          <!-- Patient Search -->
          <mat-form-field appearance="outline">
            <mat-label>Bệnh nhân</mat-label>
            <input matInput [formControl]="patientSearchControl" placeholder="Tìm bệnh nhân..."
                   aria-label="Tìm kiếm bệnh nhân" [matAutocomplete]="patientAuto">
            <mat-icon matSuffix>search</mat-icon>
            <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientFn">
              @for (p of filteredPatients; track p.id) {
              <mat-option [value]="p" (onSelectionChange)="onPatientSelected(p)">
                {{ p.fullName }} - {{ p.id | slice:0:8 }}...
              </mat-option>
              }
            </mat-autocomplete>
            @if (prescriptionForm.get('patientId')?.hasError('required')) {
            <mat-error>Vui lòng chọn bệnh nhân</mat-error>
            }
          </mat-form-field>

          <!-- Medication Search -->
          <mat-form-field appearance="outline">
            <mat-label>Thuốc</mat-label>
            <mat-select formControlName="medicationId" required aria-label="Chọn thuốc">
              @for (med of medications; track med.id) {
              <mat-option [value]="med.id">
                {{ med.name }} - {{ med.strength }} ({{ med.dosageForm }})
              </mat-option>
              }
            </mat-select>
            @if (prescriptionForm.get('medicationId')?.hasError('required')) {
            <mat-error>Vui lòng chọn thuốc</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Đường dùng</mat-label>
            <mat-select formControlName="route" required aria-label="Chọn đường dùng">
              <mat-option value="Uống">Uống</mat-option>
              <mat-option value="Tiêm tĩnh mạch">Tiêm tĩnh mạch</mat-option>
              <mat-option value="Tiêm bắp">Tiêm bắp</mat-option>
              <mat-option value="Tiêm dưới da">Tiêm dưới da</mat-option>
              <mat-option value="Bôi ngoài da">Bôi ngoài da</mat-option>
              <mat-option value="Nhỏ mắt">Nhỏ mắt</mat-option>
              <mat-option value="Xịt mũi">Xịt mũi</mat-option>
              <mat-option value="Đặt trực tràng">Đặt trực tràng</mat-option>
              <mat-option value="Hít">Hít</mat-option>
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Hướng dẫn sử dụng</mat-label>
            <input matInput formControlName="dosageInstructions" required
                   placeholder="VD: Uống 1 viên x 2 lần/ngày sau ăn"
                   aria-label="Hướng dẫn sử dụng">
            @if (prescriptionForm.get('dosageInstructions')?.hasError('required')) {
            <mat-error>Vui lòng nhập hướng dẫn</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Số lượng</mat-label>
            <input matInput formControlName="quantity" type="number" required min="1"
                   aria-label="Số lượng thuốc">
            @if (prescriptionForm.get('quantity')?.hasError('required')) {
            <mat-error>Vui lòng nhập số lượng</mat-error>
            }
            @if (prescriptionForm.get('quantity')?.hasError('min')) {
            <mat-error>Số lượng phải lớn hơn 0</mat-error>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Số lần tái kê</mat-label>
            <input matInput formControlName="refills" type="number" min="0" value="0"
                   aria-label="Số lần tái kê">
          </mat-form-field>
        </div>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/pharmacy/prescriptions">Hủy</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="prescriptionForm.invalid || submitting">
            @if (submitting) {
            <mat-spinner diameter="18" class="btn-spinner" aria-label="Đang lưu"></mat-spinner>
            }
            {{ submitting ? 'Đang lưu...' : 'Tạo đơn thuốc' }}
          </button>
        </div>
      </form>
    </div>
  `,
    styles: [`
    .prescription-form { padding: 24px; max-width: 900px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .form-actions { margin-top: 24px; display: flex; gap: 12px; justify-content: flex-end; }
    .btn-spinner { display: inline-block; margin-right: 8px; }
  `],
})
export class PrescriptionFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  submitting = false;
  medications: Medication[] = [];
  filteredPatients: Patient[] = [];
  patientSearchControl = new FormControl('');

  prescriptionForm = this.fb.group({
    patientId: ['', Validators.required],
    providerId: ['', Validators.required],
    medicationId: ['', Validators.required],
    route: ['Uống', Validators.required],
    dosageInstructions: ['', Validators.required],
    quantity: [1, [Validators.required, Validators.min(1)]],
    refills: [0, [Validators.min(0)]],
  });

  constructor(
    private fb: FormBuilder,
    private pharmacyService: PharmacyService,
    private patientService: PatientService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    // Load medications list
    this.pharmacyService.searchMedications({ pageSize: 200 })
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        this.medications = result.items;
        this.cdr.markForCheck();
      });

    // Patient search with debounce
    this.patientSearchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntil(this.destroy$),
    ).subscribe((term) => {
      const searchTerm = term ?? '';
      if (searchTerm.length < 2) {
        this.filteredPatients = [];
        this.cdr.markForCheck();
        return;
      }
      this.patientService.search(searchTerm, 1, 20)
        .pipe(takeUntil(this.destroy$))
        .subscribe((result) => {
          this.filteredPatients = result.items;
          this.cdr.markForCheck();
        });
    });

    // Set provider from current user (simplified - in real app would get from auth)
    this.prescriptionForm.patchValue({ providerId: 'current-provider' });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  displayPatientFn(patient: Patient): string {
    return patient ? `${patient.fullName} (${patient.id.slice(0, 8)}...)` : '';
  }

  onPatientSelected(patient: Patient): void {
    this.prescriptionForm.patchValue({ patientId: patient.id });
  }

  onSubmit(): void {
    if (this.prescriptionForm.invalid) return;

    this.submitting = true;
    const request = this.prescriptionForm.value as any;

    this.pharmacyService.createPrescription(request)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (prescription) => {
          this.snackBar.open('Đã tạo đơn thuốc thành công', 'Đóng', { duration: 3000 });
          this.router.navigate(['/pharmacy/prescriptions', prescription.id]);
        },
        error: () => {
          this.submitting = false;
          this.snackBar.open('Không thể tạo đơn thuốc', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
