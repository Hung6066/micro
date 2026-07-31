import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
import { FormBuilder, FormGroup, FormArray, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { AdminService } from '@core/services/admin.service';
import { AdminUser, Role } from '@core/models/admin.model';
import {
  HisHopeCreateDialogShellComponent, HisHopeTranslatePipe, HisHopeI18nService,
} from '@his-hope/frontend-foundation';

export interface AssignRolesData {
  user: AdminUser;
}

@Component({
    selector: 'app-assign-roles-dialog',
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatCheckboxModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatSnackBarModule,
        HisHopeCreateDialogShellComponent, HisHopeTranslatePipe,
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    templateUrl: './assign-roles.dialog.html',
    styleUrls: ['./assign-roles.dialog.scss']
})
export class AssignRolesDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);

  roles: Role[] = [];
  roleCheckboxes: import('@angular/forms').FormControl[];
  saving = false;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<AssignRolesDialogComponent>,
    private adminService: AdminService,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: AssignRolesData,
  ) {
    this.roleCheckboxes = [];
    this.loadRoles();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadRoles(): void {
    this.adminService.getRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.roles = roles;
          this.roleCheckboxes = roles.map(
            (r) => this.fb.control(this.data.user.roles.includes(r.name)),
          );
          this.cdr.markForCheck();
        },
      });
  }

  save(): void {
    if (this.saving) return;
    this.saving = true;
    this.cdr.markForCheck();

    const selectedRoleIds = this.roles
      .filter((_, i) => this.roleCheckboxes[i].value)
      .map((r) => r.name);

    this.adminService.assignRoles(this.data.user.id, selectedRoleIds)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(this.i18n.t('assignRoles.saveSuccess', 'Đã cập nhật vai trò thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: () => {
          this.saving = false;
          this.snackBar.open(this.i18n.t('assignRoles.saveFailed', 'Không thể cập nhật vai trò'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
