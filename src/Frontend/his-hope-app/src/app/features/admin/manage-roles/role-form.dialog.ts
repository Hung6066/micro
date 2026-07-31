import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormArray, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { AdminService } from '@core/services/admin.service';
import { Role, PermissionGroup, Permission } from '@core/models/admin.model';
import {
  HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent,
  HisHopeFormSectionComponent, HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

export interface RoleFormData {
  role?: Role;
  mode: 'create' | 'edit';
}

@Component({
    selector: 'app-role-form-dialog',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatCheckboxModule,
        MatExpansionModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatSnackBarModule,
        HisHopeCreateDialogShellComponent, HisHopeFormLayoutComponent,
        HisHopeFormSectionComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './role-form.dialog.html',
    styleUrls: ['./role-form.dialog.scss']
})
export class RoleFormDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

  form: FormGroup;
  saving = false;
  permissionGroups: PermissionGroup[] = [];
  selectedPermissions: Set<string> = new Set();

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<RoleFormDialogComponent>,
    private adminService: AdminService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: RoleFormData,
  ) {
    this.form = this.fb.group({
      name: [data.role?.name || '', Validators.required],
      description: [data.role?.description || '', Validators.required],
    });

    if (data.role?.permissions) {
      this.selectedPermissions = new Set(data.role.permissions);
    }

    this.loadPermissions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadPermissions(): void {
    this.adminService.getPermissions()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.permissionGroups = groups;
          this.cdr.markForCheck();
        },
      });
  }

  isPermissionSelected(code: string): boolean {
    return this.selectedPermissions.has(code);
  }

  isGroupFullySelected(group: PermissionGroup): boolean {
    return group.permissions.every((p) => this.selectedPermissions.has(p.code));
  }

  isGroupPartiallySelected(group: PermissionGroup): boolean {
    const selected = group.permissions.filter((p) => this.selectedPermissions.has(p.code));
    return selected.length > 0 && selected.length < group.permissions.length;
  }

  togglePermission(code: string, checked: boolean): void {
    if (checked) {
      this.selectedPermissions.add(code);
    } else {
      this.selectedPermissions.delete(code);
    }
    this.cdr.markForCheck();
  }

  toggleGroup(checked: boolean, group: PermissionGroup): void {
    for (const perm of group.permissions) {
      if (checked) {
        this.selectedPermissions.add(perm.code);
      } else {
        this.selectedPermissions.delete(perm.code);
      }
    }
    this.cdr.markForCheck();
  }

  save(): void {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    this.cdr.markForCheck();

    const payload = {
      name: this.form.value.name,
      description: this.form.value.description,
      permissions: Array.from(this.selectedPermissions),
    };

    const obs$ = this.data.mode === 'create'
      ? this.adminService.createRole(payload)
      : this.adminService.updateRole(this.data.role!.id, payload);

    obs$.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(
            this.data.mode === 'create' ? 'Đã thêm vai trò thành công' : 'Đã cập nhật vai trò thành công',
            'Đóng', { duration: 3000 },
          );
          this.dialogRef.close(true);
        },
        error: () => {
          this.saving = false;
          this.snackBar.open('Không thể lưu vai trò', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
