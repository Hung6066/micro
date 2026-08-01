import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, Validators, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { PharmacyService } from '@core/services/pharmacy.service';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import {
  HisHopePageLayoutComponent, HisHopePageHeaderComponent,
  HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
    selector: 'app-medication-form',
    standalone: true,
    imports: [
        CommonModule, ReactiveFormsModule, RouterModule,
        MatFormFieldModule, MatInputModule, MatSelectModule,
        MatButtonModule, MatProgressSpinnerModule, MatCheckboxModule,
        MatSnackBarModule,
        HisHopePageLayoutComponent, HisHopePageHeaderComponent,
        HisHopeFormLayoutComponent, HisHopeFormSectionComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader
                      [title]="(isEdit ? 'medicationForm.title.edit' : 'medicationForm.title.create') | hhTranslate:(isEdit ? 'Chỉnh sửa thuốc' : 'Thêm thuốc mới')"
                      [subtitle]="'medicationForm.subtitle' | hhTranslate:'Quản lý thông tin thuốc'" />

      <form [formGroup]="medicationForm" (ngSubmit)="onSubmit()" novalidate>
        <hh-form-layout>
          <hh-form-section [title]="'medicationForm.section.basic' | hhTranslate:'Thông tin thuốc'" [span]="2">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>{{ 'medicationForm.name' | hhTranslate:'Tên thuốc' }}</mat-label>
                <input matInput formControlName="name" required [placeholder]="'medicationForm.namePlaceholder' | hhTranslate:'VD: Paracetamol 500mg'">
                @if (medicationForm.get('name')?.hasError('required')) {
                <mat-error>{{ 'medicationForm.nameRequired' | hhTranslate:'Vui lòng nhập tên thuốc' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'medicationForm.genericName' | hhTranslate:'Hoạt chất' }}</mat-label>
                <input matInput formControlName="genericName" required [placeholder]="'medicationForm.genericNamePlaceholder' | hhTranslate:'VD: Paracetamol'">
                @if (medicationForm.get('genericName')?.hasError('required')) {
                <mat-error>{{ 'medicationForm.genericNameRequired' | hhTranslate:'Vui lòng nhập hoạt chất' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'medicationForm.brandName' | hhTranslate:'Tên thương mại' }}</mat-label>
                <input matInput formControlName="brandName" [placeholder]="'medicationForm.brandNamePlaceholder' | hhTranslate:'VD: Panadol'">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'medicationForm.strength' | hhTranslate:'Hàm lượng' }}</mat-label>
                <input matInput formControlName="strength" required [placeholder]="'medicationForm.strengthPlaceholder' | hhTranslate:'VD: 500mg'">
                @if (medicationForm.get('strength')?.hasError('required')) {
                <mat-error>{{ 'medicationForm.strengthRequired' | hhTranslate:'Vui lòng nhập hàm lượng' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'medicationForm.dosageForm' | hhTranslate:'Dạng bào chế' }}</mat-label>
                <mat-select formControlName="dosageForm" required>
                  <mat-option value="tablet">{{ 'medicationForm.dosageForm.tablet' | hhTranslate:'Viên nén' }}</mat-option>
                  <mat-option value="capsule">{{ 'medicationForm.dosageForm.capsule' | hhTranslate:'Viên nang' }}</mat-option>
                  <mat-option value="solution">{{ 'medicationForm.dosageForm.solution' | hhTranslate:'Dung dịch' }}</mat-option>
                  <mat-option value="suspension">{{ 'medicationForm.dosageForm.suspension' | hhTranslate:'Hỗn dịch' }}</mat-option>
                  <mat-option value="powder">{{ 'medicationForm.dosageForm.powder' | hhTranslate:'Bột pha' }}</mat-option>
                  <mat-option value="cream">{{ 'medicationForm.dosageForm.cream' | hhTranslate:'Kem bôi' }}</mat-option>
                  <mat-option value="ointment">{{ 'medicationForm.dosageForm.ointment' | hhTranslate:'Thuốc mỡ' }}</mat-option>
                  <mat-option value="injection">{{ 'medicationForm.dosageForm.injection' | hhTranslate:'Ống tiêm' }}</mat-option>
                  <mat-option value="inhalation">{{ 'medicationForm.dosageForm.inhalation' | hhTranslate:'Khí dung' }}</mat-option>
                </mat-select>
                @if (medicationForm.get('dosageForm')?.hasError('required')) {
                <mat-error>{{ 'medicationForm.dosageFormRequired' | hhTranslate:'Vui lòng chọn dạng bào chế' }}</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>{{ 'medicationForm.route' | hhTranslate:'Đường dùng' }}</mat-label>
                <mat-select formControlName="route" required>
                  <mat-option value="oral">{{ 'medicationForm.route.oral' | hhTranslate:'Uống' }}</mat-option>
                  <mat-option value="iv">{{ 'medicationForm.route.iv' | hhTranslate:'Tiêm tĩnh mạch' }}</mat-option>
                  <mat-option value="im">{{ 'medicationForm.route.im' | hhTranslate:'Tiêm bắp' }}</mat-option>
                  <mat-option value="sc">{{ 'medicationForm.route.sc' | hhTranslate:'Tiêm dưới da' }}</mat-option>
                  <mat-option value="topical">{{ 'medicationForm.route.topical' | hhTranslate:'Bôi ngoài da' }}</mat-option>
                  <mat-option value="eye">{{ 'medicationForm.route.eye' | hhTranslate:'Nhỏ mắt' }}</mat-option>
                  <mat-option value="nasal">{{ 'medicationForm.route.nasal' | hhTranslate:'Xịt mũi' }}</mat-option>
                  <mat-option value="rectal">{{ 'medicationForm.route.rectal' | hhTranslate:'Đặt trực tràng' }}</mat-option>
                  <mat-option value="inhaled">{{ 'medicationForm.route.inhaled' | hhTranslate:'Hít' }}</mat-option>
                </mat-select>
                @if (medicationForm.get('route')?.hasError('required')) {
                <mat-error>{{ 'medicationForm.routeRequired' | hhTranslate:'Vui lòng chọn đường dùng' }}</mat-error>
                }
              </mat-form-field>

              <mat-checkbox formControlName="requiresPrescription" class="full-width">
                {{ 'medicationForm.requiresPrescription' | hhTranslate:'Yêu cầu kê đơn' }}
              </mat-checkbox>
            </div>
          </hh-form-section>
        </hh-form-layout>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/pharmacy/medications">{{ 'common.cancel' | hhTranslate:'Hủy' }}</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="medicationForm.invalid || submitting">
            @if (submitting) {
            <mat-spinner diameter="18" class="btn-spinner" [attr.aria-label]="'common.saving' | hhTranslate:'Đang lưu'"></mat-spinner>
            }
            {{ submitting ? ('common.saving' | hhTranslate:'Đang lưu...') : (isEdit ? ('medicationForm.save.edit' | hhTranslate:'Cập nhật thuốc') : ('medicationForm.save.create' | hhTranslate:'Thêm thuốc')) }}
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
export class MedicationFormComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  isEdit = false;
  medicationId?: string;
  submitting = false;

  medicationForm = this.fb.group({
    name: ['', Validators.required],
    genericName: ['', Validators.required],
    brandName: [''],
    dosageForm: ['', Validators.required],
    strength: ['', Validators.required],
    route: ['', Validators.required],
    requiresPrescription: [false],
  });

  constructor(
    private fb: FormBuilder,
    private pharmacyService: PharmacyService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.medicationId = this.route.snapshot.params['id'];
    if (this.medicationId) {
      this.isEdit = true;
      this.pharmacyService.getMedication(this.medicationId)
        .pipe(takeUntil(this.destroy$))
        .subscribe((med) => {
          this.medicationForm.patchValue(med as any);
          this.cdr.markForCheck();
        });
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSubmit(): void {
    if (this.medicationForm.invalid) return;

    this.submitting = true;
    const request = this.medicationForm.value as any;

    const action = this.isEdit
      ? this.pharmacyService.updateMedication(this.medicationId!, request)
      : this.pharmacyService.createMedication(request);

    action.pipe(takeUntil(this.destroy$)).subscribe({
      next: (medication) => {
        this.snackBar.open(
          `Đã ${this.isEdit ? 'cập nhật' : 'thêm'} thuốc thành công`,
          'Đóng', { duration: 3000 },
        );
        this.router.navigate(['/pharmacy/medications', medication.id]);
      },
      error: () => {
        this.submitting = false;
        this.snackBar.open('Không thể lưu thuốc', 'Đóng', { duration: 5000 });
        this.cdr.markForCheck();
      },
    });
  }
}
