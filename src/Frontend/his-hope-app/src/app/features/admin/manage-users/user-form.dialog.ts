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
import { AdminService } from '@core/services/admin.service';
import { AdminUser } from '@core/models/admin.model';

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
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <h2 mat-dialog-title>{{ data.mode === 'create' ? 'Thêm người dùng' : 'Chỉnh sửa người dùng' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Họ và tên</mat-label>
          <input matInput formControlName="fullName" placeholder="Nhập họ và tên" required>
          <mat-error *ngIf="form.get('fullName')?.hasError('required')">Vui lòng nhập họ tên</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Email</mat-label>
          <input matInput formControlName="email" placeholder="email@example.com" required>
          <mat-error *ngIf="form.get('email')?.hasError('required')">Vui lòng nhập email</mat-error>
          <mat-error *ngIf="form.get('email')?.hasError('email')">Email không hợp lệ</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Số điện thoại</mat-label>
          <input matInput formControlName="phone" placeholder="090xxxxxxx" required>
          <mat-error *ngIf="form.get('phone')?.hasError('required')">Vui lòng nhập số điện thoại</mat-error>
          <mat-error *ngIf="form.get('phone')?.hasError('pattern')">Số điện thoại không hợp lệ (10-11 số)</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" *ngIf="data.mode === 'create'">
          <mat-label>Mật khẩu</mat-label>
          <input matInput type="password" formControlName="password" placeholder="Nhập mật khẩu" required>
          <mat-error *ngIf="form.get('password')?.hasError('required')">Vui lòng nhập mật khẩu</mat-error>
          <mat-error *ngIf="form.get('password')?.hasError('minlength')">Mật khẩu tối thiểu 6 ký tự</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Vai trò</mat-label>
          <mat-select formControlName="roles" multiple required>
            <mat-option *ngFor="let r of roleOptions" [value]="r.value">{{ r.label }}</mat-option>
          </mat-select>
          <mat-error *ngIf="form.get('roles')?.hasError('required')">Vui lòng chọn ít nhất một vai trò</mat-error>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        <mat-icon>{{ data.mode === 'create' ? 'person_add' : 'save' }}</mat-icon>
        <span *ngIf="!saving">{{ data.mode === 'create' ? 'Thêm người dùng' : 'Lưu thay đổi' }}</span>
        <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
      </button>
    </mat-dialog-actions>
  `,
    styles: [`
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 420px; padding-top: 8px; }
  `]
})
export class UserFormDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

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
            this.snackBar.open('Đã thêm người dùng thành công', 'Đóng', { duration: 3000 });
            this.dialogRef.close(true);
          },
          error: () => {
            this.saving = false;
            this.snackBar.open('Không thể thêm người dùng', 'Đóng', { duration: 5000 });
            this.cdr.markForCheck();
          },
        });
    } else {
      const { password, ...updateData } = formValue;
      this.adminService.updateUser(this.data.user!.id, updateData)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.snackBar.open('Đã cập nhật người dùng thành công', 'Đóng', { duration: 3000 });
            this.dialogRef.close(true);
          },
          error: () => {
            this.saving = false;
            this.snackBar.open('Không thể cập nhật người dùng', 'Đóng', { duration: 5000 });
            this.cdr.markForCheck();
          },
        });
    }
  }
}
