import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { Role } from '@core/models/admin.model';
import { RoleFormDialogComponent, RoleFormData } from './role-form.dialog';
import {
  HisHopeConfirmDialogComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableCellDirective,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-manage-roles',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule, MatIconModule, MatMenuModule,
    MatTooltipModule, MatDialogModule, MatSnackBarModule,
    HisHopeDataTableComponent, HisHopeDataTableCellDirective, HisHopeConfirmDialogComponent,
    HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './manage-roles.component.html',
  styleUrls: ['./manage-roles.component.scss'],
})
export class ManageRolesComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  roles: Role[] = [];
  loading = true;
  error: string | null = null;

  readonly columns: HisHopeDataTableColumn[] = [
    { key: 'name', label: 'Tên vai trò' },
    { key: 'usersCount', label: 'Người dùng' },
    { key: 'system', label: 'Hệ thống' },
    { key: 'permissions', label: 'Quyền', computed: (row) => `${row['permissionCount']} quyền` },
    { key: 'actions', label: 'Thao tác' },
  ];
  rows: Record<string, unknown>[] = [];

  // Confirm dialog state (replaces MatDialog-based ConfirmDialogComponent)
  showConfirmDialog = false;
  confirmDialogTitle = '';
  confirmDialogMessage = '';
  confirmDialogConfirmLabel = '';
  private pendingRoleDelete: Role | null = null;

  constructor(
    private adminService: AdminService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadRoles();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadRoles(): void {
    this.loading = true;
    this.error = null;
    this.cdr.markForCheck();

    this.adminService.getRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.roles = roles;
          this.rows = roles.map((r) => ({
            id: r.id,
            name: r.name,
            description: r.description,
            usersCount: r.usersCount,
            isSystem: r.isSystem,
            permissionCount: r.permissions.length,
            entity: r,
          }));
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.error = 'Không thể tải danh sách vai trò';
          this.snackBar.open('Không thể tải danh sách vai trò', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  getRoleLabel(name: string): string {
    const labels: Record<string, string> = {
      admin: 'Quản trị viên',
      doctor: 'Bác sĩ',
      nurse: 'Điều dưỡng',
      pharmacist: 'Dược sĩ',
      receptionist: 'Lễ tân',
      manager: 'Quản lý',
    };
    return labels[name] || name;
  }

  roleFromRow(row: Record<string, unknown>): Role {
    return row['entity'] as Role;
  }

  openAddRoleDialog(): void {
    const dialogRef = this.dialog.open<RoleFormDialogComponent, RoleFormData>(RoleFormDialogComponent, {
      width: '640px',
      maxHeight: '90vh',
      data: { mode: 'create' },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadRoles();
      });
  }

  openEditRoleDialog(role: Role): void {
    const dialogRef = this.dialog.open<RoleFormDialogComponent, RoleFormData>(RoleFormDialogComponent, {
      width: '640px',
      maxHeight: '90vh',
      data: { role, mode: 'edit' },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadRoles();
      });
  }

  deleteRole(role: Role): void {
    if (role.isSystem) return;

    this.confirmDialogTitle = 'Xóa vai trò';
    this.confirmDialogMessage = `Bạn có chắc muốn xóa vai trò "${this.getRoleLabel(role.name)}"? Hành động này không thể hoàn tác.`;
    this.confirmDialogConfirmLabel = 'Xóa';
    this.pendingRoleDelete = role;
    this.showConfirmDialog = true;
    this.cdr.markForCheck();
  }

  onConfirmDialogConfirmed(): void {
    this.showConfirmDialog = false;
    const role = this.pendingRoleDelete;
    this.pendingRoleDelete = null;
    if (!role) return;

    this.adminService.deleteRole(role.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open('Đã xóa vai trò thành công', 'Đóng', { duration: 3000 });
          this.loadRoles();
        },
        error: () => {
          this.snackBar.open('Không thể xóa vai trò', 'Đóng', { duration: 5000 });
        },
      });
  }

  onConfirmDialogCancelled(): void {
    this.showConfirmDialog = false;
    this.pendingRoleDelete = null;
  }
}
