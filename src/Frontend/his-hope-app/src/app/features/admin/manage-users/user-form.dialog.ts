import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
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
import { AdminService } from '@core/services/admin.service';
import { AdminUser } from '@core/models/admin.model';
import {
  HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent, HisHopeTranslatePipe, HisHopeI18nService,
} from '@his-hope/frontend-foundation';

export interface UserFormData {
  user?: AdminUser;
  mode: 'create' | 'edit';
}

const ROLE_OPTIONS = [
  { value: 'admin', label: 'Quản trị viên' },
  { value: 'doctor', label: 'Bác sĩ' },
  { value: 'nurse', label: 'Điều dưỡng' },
  { value: 'pharmacist', label: 'Dược sĩ' },
  { value: 'receptionist', label: 'Lễ tân' },
  { value: 'manager', label: 'Quản lý' },
];

@Component({
    selector: 'app-user-form-dialog',
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
        HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent,
        HisHopeFormSectionComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './user-form.dialog.html',
    styleUrls: ['./user-form.dialog.scss']
})
export class UserFormDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  form: FormGroup;
  saving = false;
  roleOptions = ROLE_OPTIONS;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<UserFormDialogComponent>,
    private adminService: AdminService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: UserFormData,
  ) {
    const user = data.user;
    this.form = this.fb.group({
      fullName: [user?.fullName || '', Validators.required],
      email: [user?.email || '', [Validators.required, Validators.email]],
      phone: [user?.phone || '', [Validators.required, Validators.pattern(/^0[0-9]{9,10}$/)]],
      password: ['', data.mode === 'create' ? [Validators.required, Validators.minLength(6)] : []],
      roles: [user?.roles || [], Validators.required],
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

    const formValue = this.form.value;

    if (this.data.mode === 'create') {
      this.adminService.createUser(formValue)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.snackBar.open(this.i18n.t('userForm.createSuccess', 'Đã thêm người dùng thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
            this.dialogRef.close(true);
          },
          error: () => {
            this.saving = false;
            this.snackBar.open(this.i18n.t('userForm.createFailed', 'Không thể thêm người dùng'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
            this.cdr.markForCheck();
          },
        });
    } else {
      const { password, ...updateData } = formValue;
      this.adminService.updateUser(this.data.user!.id, updateData)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.snackBar.open(this.i18n.t('userForm.updateSuccess', 'Đã cập nhật người dùng thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
            this.dialogRef.close(true);
          },
          error: () => {
            this.saving = false;
            this.snackBar.open(this.i18n.t('userForm.updateFailed', 'Không thể cập nhật người dùng'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
            this.cdr.markForCheck();
          },
        });
    }
  }
}
