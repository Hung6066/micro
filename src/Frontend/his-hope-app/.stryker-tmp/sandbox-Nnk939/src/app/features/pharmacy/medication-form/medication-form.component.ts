// @ts-nocheck
import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { PharmacyService } from '@core/services/pharmacy.service';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-medication-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="medication-form">
      <h1>{{ isEdit ? 'Chỉnh sửa thuốc' : 'Thêm thuốc mới' }}</h1>

      <form [formGroup]="medicationForm" (ngSubmit)="onSubmit()">
        <div class="form-grid">
          <mat-form-field appearance="outline">
            <mat-label>Tên thuốc</mat-label>
            <input matInput formControlName="name" required placeholder="VD: Paracetamol 500mg">
            <mat-error *ngIf="medicationForm.get('name')?.hasError('required')">Vui lòng nhập tên thuốc</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Hoạt chất</mat-label>
            <input matInput formControlName="genericName" required placeholder="VD: Paracetamol">
            <mat-error *ngIf="medicationForm.get('genericName')?.hasError('required')">Vui lòng nhập hoạt chất</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Tên thương mại</mat-label>
            <input matInput formControlName="brandName" placeholder="VD: Panadol">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Hàm lượng</mat-label>
            <input matInput formControlName="strength" required placeholder="VD: 500mg">
            <mat-error *ngIf="medicationForm.get('strength')?.hasError('required')">Vui lòng nhập hàm lượng</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Dạng bào chế</mat-label>
            <mat-select formControlName="dosageForm" required>
              <mat-option value="Viên nén">Viên nén</mat-option>
              <mat-option value="Viên nang">Viên nang</mat-option>
              <mat-option value="Dung dịch">Dung dịch</mat-option>
              <mat-option value="Hỗn dịch">Hỗn dịch</mat-option>
              <mat-option value="Bột pha">Bột pha</mat-option>
              <mat-option value="Kem bôi">Kem bôi</mat-option>
              <mat-option value="Thuốc mỡ">Thuốc mỡ</mat-option>
              <mat-option value="Ống tiêm">Ống tiêm</mat-option>
              <mat-option value="Khí dung">Khí dung</mat-option>
            </mat-select>
            <mat-error *ngIf="medicationForm.get('dosageForm')?.hasError('required')">Vui lòng chọn dạng bào chế</mat-error>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Đường dùng</mat-label>
            <mat-select formControlName="route" required>
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
            <mat-error *ngIf="medicationForm.get('route')?.hasError('required')">Vui lòng chọn đường dùng</mat-error>
          </mat-form-field>

          <mat-checkbox formControlName="requiresPrescription" class="full-width">
            Yêu cầu kê đơn
          </mat-checkbox>
        </div>

        <div class="form-actions">
          <button mat-button type="button" routerLink="/pharmacy/medications">Hủy</button>
          <button mat-raised-button color="primary" type="submit"
                  [disabled]="medicationForm.invalid || submitting">
            <mat-spinner diameter="18" *ngIf="submitting" class="btn-spinner" aria-label="Đang lưu"></mat-spinner>
            {{ submitting ? 'Đang lưu...' : (isEdit ? 'Cập nhật thuốc' : 'Thêm thuốc') }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .medication-form { padding: 24px; max-width: 900px; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .full-width { grid-column: 1 / -1; margin: 8px 0; }
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
