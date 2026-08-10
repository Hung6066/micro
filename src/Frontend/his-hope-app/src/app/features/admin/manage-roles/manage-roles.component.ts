import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
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
  HisHopeI18nService,
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
  private readonly i18n = inject(HisHopeI18nService);

  roles: Role[] = [];
  loading = true;
  error: string | null = null;

  readonly columns: HisHopeDataTableColumn[] = [
    { key: 'name', label: this.i18n.t('manageRoles.column.name', 'Tên vai trò') },
    { key: 'usersCount', label: this.i18n.t('manageRoles.column.users', 'Người dùng') },
    { key: 'system', label: this.i18n.t('manageRoles.column.system', 'Hệ thống') },
    { key: 'permissions', label: this.i18n.t('manageRoles.column.permissions', 'Quyền'), computed: (row) => this.i18n.t('manageRoles.permissionCount', '{{count}} quyền', { count: row['permissionCount'] as number }) },
    { key: 'actions', label: this.i18n.t('manageRoles.column.actions', 'Thao tác') },
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
          this.error = this.i18n.t('manageRoles.loadFailed', 'Không thể tải danh sách vai trò');
          this.snackBar.open(this.error, this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  getRoleLabel(name: string): string {
    return this.i18n.t(`manageRoles.role.${name}`, name);
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

    this.confirmDialogTitle = this.i18n.t('manageRoles.confirmDeleteTitle', 'Xóa vai trò');
    this.confirmDialogMessage = this.i18n.t('manageRoles.confirmDeleteMessage', 'Bạn có chắc muốn xóa vai trò "{{name}}"? Hành động này không thể hoàn tác.', { name: this.getRoleLabel(role.name) });
    this.confirmDialogConfirmLabel = this.i18n.t('common.delete', 'Xóa');
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
          this.snackBar.open(this.i18n.t('manageRoles.deleteSuccess', 'Đã xóa vai trò thành công'), this.i18n.t('common.close', 'Đóng'), { duration: 3000 });
          this.loadRoles();
        },
        error: () => {
          this.snackBar.open(this.i18n.t('manageRoles.deleteFailed', 'Không thể xóa vai trò'), this.i18n.t('common.close', 'Đóng'), { duration: 5000 });
        },
      });
  }

  onConfirmDialogCancelled(): void {
    this.showConfirmDialog = false;
    this.pendingRoleDelete = null;
  }
}
