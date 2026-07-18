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
    ],
    changeDetection: ChangeDetectionStrategy.OnPush,
    template: `
    <h2 mat-dialog-title>{{ data.mode === 'create' ? 'Thêm vai trò' : 'Chỉnh sửa vai trò' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="dialog-form">
        <mat-form-field appearance="outline">
          <mat-label>Tên vai trò</mat-label>
          <input matInput formControlName="name" placeholder="Nhập tên vai trò" required>
          @if (form.get('name')?.hasError('required')) {
          <mat-error>Vui lòng nhập tên vai trò</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Mô tả</mat-label>
          <textarea matInput formControlName="description" rows="3" placeholder="Mô tả vai trò..." required></textarea>
          @if (form.get('description')?.hasError('required')) {
          <mat-error>Vui lòng nhập mô tả</mat-error>
          }
        </mat-form-field>

        <div class="section-label">
          <mat-icon>security</mat-icon>
          <span>Ma trận quyền</span>
        </div>

        @if (permissionGroups.length > 0) {
        <mat-accordion class="permission-accordion">
          @for (group of permissionGroups; track group.groupName) {
          <mat-expansion-panel expanded>
            <mat-expansion-panel-header>
              <mat-panel-title>
                <mat-checkbox [checked]="isGroupFullySelected(group)" [indeterminate]="isGroupPartiallySelected(group)"
                              (change)="toggleGroup($event.checked, group)"
                              (click)="$event.stopPropagation()">
                  {{ group.groupName }}
                </mat-checkbox>
              </mat-panel-title>
              <mat-panel-description>
                {{ group.permissions.length }} quyền
              </mat-panel-description>
            </mat-expansion-panel-header>
            <div class="perm-grid">
              @for (perm of group.permissions; track perm.code) {
              <div class="perm-item">
                <mat-checkbox [checked]="isPermissionSelected(perm.code)"
                              (change)="togglePermission(perm.code, $event.checked)">
                  <div class="perm-info">
                    <span class="perm-name">{{ perm.name }}</span>
                    <span class="perm-desc">{{ perm.description }}</span>
                  </div>
                </mat-checkbox>
              </div>
              }
            </div>
          </mat-expansion-panel>
          }
        </mat-accordion>
        } @else {
        <div class="loading-state">
          <mat-spinner diameter="24"></mat-spinner>
          <span>Đang tải quyền...</span>
        </div>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        <mat-icon>{{ data.mode === 'create' ? 'add_moderator' : 'save' }}</mat-icon>
        @if (!saving) {
        <span>{{ data.mode === 'create' ? 'Thêm vai trò' : 'Lưu thay đổi' }}</span>
        }
        @if (saving) {
        <mat-spinner diameter="20"></mat-spinner>
        }
      </button>
    </mat-dialog-actions>
  `,
    styles: [`
    .dialog-form { display: flex; flex-direction: column; gap: 16px; min-width: 520px; padding-top: 8px; }
    .section-label { display: flex; align-items: center; gap: 8px; font-weight: 600; color: #1A1A1A; margin-top: 8px; }
    .section-label mat-icon { color: var(--mat-sys-primary); }
    .permission-accordion { margin-top: 4px; }
    .perm-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 8px; padding: 4px 0; }
    .perm-item { padding: 4px 0; }
    .perm-info { display: flex; flex-direction: column; margin-left: 4px; }
    .perm-name { font-weight: 500; font-size: 13px; color: #1A1A1A; }
    .perm-desc { font-size: 11px; color: #787774; margin-top: 1px; }
    .loading-state { display: flex; align-items: center; gap: 12px; justify-content: center; padding: 48px; color: #787774; }
    ::ng-deep .mat-expansion-panel-header-title .mat-checkbox { pointer-events: auto; }
    ::ng-deep .mat-expansion-panel-header-description { justify-content: flex-end; margin-right: 0; }
  `]
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
