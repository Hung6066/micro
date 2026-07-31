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
import {
  HisHopePageLayoutComponent, HisHopePageHeaderComponent,
  HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-prescription-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatFormFieldModule, MatInputModule, MatIconModule, MatSelectModule,
        MatButtonModule, MatAutocompleteModule, MatProgressSpinnerModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent,
        HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="'prescriptionForm.title' | hhTranslate:'Tạo đơn thuốc mới'"
                      [subtitle]="'prescriptionForm.subtitle' | hhTranslate:'Kê đơn thuốc cho bệnh nhân'" />

      <form [formGroup]="prescriptionForm" (ngSubmit)="onSubmit()" novalidate>
        <hh-form-layout>
          <hh-form-section [title]="'prescriptionForm.section.basic' | hhTranslate:'Thông tin đơn thuốc'" [span]="2">
            <div class="form-grid">
              <!-- Patient Search -->
              <mat-form-field appearance="outline">
                <mat-label>{{ 'prescriptionForm.patient' | hhTranslate:'Bệnh nhân' }}</mat-label>
                <input matInput [formControl]="patientSearchControl" [placeholder]="'prescriptionForm.patientPlaceholder' | hhTranslate:'Tìm bệnh nhân...'"
                       [attr.aria-label]="'prescriptionForm.patientSearchAria' | hhTranslate:'Tìm kiếm bệnh nhân'" [matAutocomplete]="patientAuto">
                <mat-icon matSuffix>search</mat-icon>
                <mat-autocomplete #patientAuto="matAutocomplete" [displayWith]="displayPatientFn">
                  @for (p of filteredPatients; track p.id) {
                  <mat-option [value]="p" (onSelectionChange)="onPatientSelected(p)">
                    {{ p.fullName }} - {{ p.id | slice:0:8 }}...
                  </mat-option>
                  }
                </mat-autocomplete>
                @if (prescriptionForm.get('patientId')?.hasError('required')) {
                <mat-error>{{ 'prescriptionForm.patientRequired' | hhTranslate:'Vui lòng chọn bệnh nhân' }}</mat-error>
                }
              </mat-form-field>

              <!-- Medication Search -->
              <mat-form-field appearance="outline">
                <mat-label>{{ 'prescriptionForm.medication' | hhTranslate:'Thuốc' }}</mat-label>
                <mat-select formControlName="medicationId" required [attr.aria-label]="'prescriptionForm.medicationAria' | hhTranslate:'Chọn thuốc'">
                  @for (med of medications; track med.id) {
                  <mat-option [value]="med.id">
                    {{ med.name }} - {{ med.strength }} ({{ med.dosageForm }})
                  </mat-option>
                  }
                </mat-select>
                @if (prescriptionForm.get('medicationId')?.hasError('required')) {
                <mat-error>{{ 'prescriptionForm.medicationRequired' | hhTranslate:'Vui lòng chọn thuốc' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'prescriptionForm.route' | hhTranslate:'Đường dùng' }}</mat-label>
                <mat-select formControlName="route" required [attr.aria-label]="'prescriptionForm.routeAria' | hhTranslate:'Chọn đường dùng'">
                  <mat-option value="oral">{{ 'prescriptionForm.route.oral' | hhTranslate:'Uống' }}</mat-option>
                  <mat-option value="iv">{{ 'prescriptionForm.route.iv' | hhTranslate:'Tiêm tĩnh mạch' }}</mat-option>
                  <mat-option value="im">{{ 'prescriptionForm.route.im' | hhTranslate:'Tiêm bắp' }}</mat-option>
                  <mat-option value="sc">{{ 'prescriptionForm.route.sc' | hhTranslate:'Tiêm dưới da' }}</mat-option>
                  <mat-option value="topical">{{ 'prescriptionForm.route.topical' | hhTranslate:'Bôi ngoài da' }}</mat-option>
                  <mat-option value="eye">{{ 'prescriptionForm.route.eye' | hhTranslate:'Nhỏ mắt' }}</mat-option>
                  <mat-option value="nasal">{{ 'prescriptionForm.route.nasal' | hhTranslate:'Xịt mũi' }}</mat-option>
                  <mat-option value="rectal">{{ 'prescriptionForm.route.rectal' | hhTranslate:'Đặt trực tràng' }}</mat-option>
                  <mat-option value="inhaled">{{ 'prescriptionForm.route.inhaled' | hhTranslate:'Hít' }}</mat-option>
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'prescriptionForm.instructions' | hhTranslate:'Hướng dẫn sử dụng' }}</mat-label>
                <input matInput formControlName="dosageInstructions" required
                       [placeholder]="'prescriptionForm.instructionsPlaceholder' | hhTranslate:'VD: Uống 1 viên x 2 lần/ngày sau ăn'"
                       [attr.aria-label]="'prescriptionForm.instructionsAria' | hhTranslate:'Hướng dẫn sử dụng'">
                @if (prescriptionForm.get('dosageInstructions')?.hasError('required')) {
                <mat-error>{{ 'prescriptionForm.instructionsRequired' | hhTranslate:'Vui lòng nhập hướng dẫn' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'prescriptionForm.quantity' | hhTranslate:'Số lượng' }}</mat-label>
                <input matInput formControlName="quantity" type="number" required min="1"
                       [attr.aria-label]="'prescriptionForm.quantityAria' | hhTranslate:'Số lượng thuốc'">
                @if (prescriptionForm.get('quantity')?.hasError('required')) {
                <mat-error>{{ 'prescriptionForm.quantityRequired' | hhTranslate:'Vui lòng nhập số lượng' }}</mat-error>
                }
                @if (prescriptionForm.get('quantity')?.hasError('min')) {
                <mat-error>{{ 'prescriptionForm.quantityMin' | hhTranslate:'Số lượng phải lớn hơn 0' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'prescriptionForm.refills' | hhTranslate:'Số lần tái kê' }}</mat-label>
                <input matInput formControlName="refills" type="number" min="0" value="0"
                       [attr.aria-label]="'prescriptionForm.refillsAria' | hhTranslate:'Số lần tái kê'">
              </mat-form-field>
            </div>
          </hh-form-section>
        </hh-form-layout>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/pharmacy/prescriptions">{{ 'common.cancel' | hhTranslate:'Hủy' }}</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="prescriptionForm.invalid || submitting">
            @if (submitting) {
            <mat-spinner diameter="18" class="btn-spinner" [attr.aria-label]="'common.saving' | hhTranslate:'Đang lưu'"></mat-spinner>
            }
            {{ submitting ? ('common.saving' | hhTranslate:'Đang lưu...') : ('prescriptionForm.save' | hhTranslate:'Tạo đơn thuốc') }}
          </button>
        </div>
      </form>
    </hh-page-layout>
  `,
    styles: [`
    .form-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: var(--form-field-gap, 16px); }
    .form-grid .full-width { grid-column: 1 / -1; }
    @media (max-width: 768px) { .form-grid { grid-template-columns: 1fr; } }
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
    route: ['oral', Validators.required],
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
