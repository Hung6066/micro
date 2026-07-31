import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { AdminUser } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';
import { UserFormDialogComponent, UserFormData } from './user-form.dialog';
import { AssignRolesDialogComponent, AssignRolesData } from './assign-roles.dialog';
import {
  HisHopeConfirmDialogComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableCellDirective,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageQuery,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

const ROLE_FILTERS = [
  { value: '', label: 'Tất cả vai trò' },
  { value: 'admin', label: 'Quản trị viên' },
  { value: 'doctor', label: 'Bác sĩ' },
  { value: 'nurse', label: 'Điều dưỡng' },
  { value: 'pharmacist', label: 'Dược sĩ' },
  { value: 'receptionist', label: 'Lễ tân' },
  { value: 'manager', label: 'Quản lý' },
];

@Component({
  selector: 'app-manage-users',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule,
    MatButtonModule, MatIconModule,
    MatFormFieldModule, MatSelectModule, MatMenuModule,
    MatTooltipModule, MatDialogModule, MatSnackBarModule,
    HisHopeDataTableComponent, HisHopeDataTableCellDirective, HisHopeConfirmDialogComponent,
    HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss'],
})
export class ManageUsersComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  users: AdminUser[] = [];
  totalCount = 0;
  loading = true;
  error: string | null = null;
  query: HisHopePageQuery = { page: 1, pageSize: 20 };

  roleFilter = new FormControl('');

  readonly columns: HisHopeDataTableColumn[] = [
    { key: 'id', label: 'ID', computed: (row) => String(row['id']).slice(0, 12) },
    { key: 'fullName', label: 'Họ và tên' },
    { key: 'email', label: 'Email' },
    { key: 'roles', label: 'Vai trò' },
    { key: 'status', label: 'Trạng thái' },
    { key: 'actions', label: 'Thao tác' },
  ];
  rows: Record<string, unknown>[] = [];
  roleFilters = ROLE_FILTERS;

  // Confirm dialog state (replaces MatDialog-based ConfirmDialogComponent)
  showConfirmDialog = false;
  confirmDialogTitle = '';
  confirmDialogMessage = '';
  confirmDialogConfirmLabel = '';
  private pendingUserAction: { user: AdminUser; activate: boolean } | null = null;

  constructor(
    private adminService: AdminService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadUsers();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadUsers(): void {
    this.loading = true;
    this.error = null;
    this.cdr.markForCheck();

    this.adminService.getUsers({
      search: this.query.search || '',
      role: this.roleFilter.value || '',
      page: this.query.page,
      pageSize: this.query.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PagedResult<AdminUser>) => {
          this.users = result.items;
          this.totalCount = result.totalCount;
          this.rows = result.items.map((u) => ({
            id: u.id,
            fullName: u.fullName,
            email: u.email,
            roles: u.roles,
            isActive: u.isActive,
            entity: u,
          }));
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.error = 'Không thể tải danh sách người dùng';
          this.snackBar.open('Không thể tải danh sách người dùng', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.query = query;
    this.loadUsers();
  }

  onFilterChange(): void {
    this.query = { ...this.query, page: 1 };
    this.loadUsers();
  }

  getRoleLabel(role: string): string {
    const found = ROLE_FILTERS.find((f) => f.value === role);
    return found ? found.label : role;
  }

  userFromRow(row: Record<string, unknown>): AdminUser {
    return row['entity'] as AdminUser;
  }

  openAddUserDialog(): void {
    const dialogRef = this.dialog.open<UserFormDialogComponent, UserFormData>(UserFormDialogComponent, {
      width: '520px',
      data: { mode: 'create' },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadUsers();
      });
  }

  openEditUserDialog(user: AdminUser): void {
    const dialogRef = this.dialog.open<UserFormDialogComponent, UserFormData>(UserFormDialogComponent, {
      width: '520px',
      data: { user, mode: 'edit' },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadUsers();
      });
  }

  openAssignRolesDialog(user: AdminUser): void {
    const dialogRef = this.dialog.open<AssignRolesDialogComponent, AssignRolesData>(AssignRolesDialogComponent, {
      width: '520px',
      data: { user },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((result) => {
        if (result) this.loadUsers();
      });
  }

  toggleUserStatus(user: AdminUser): void {
    const action = user.isActive ? 'vô hiệu hóa' : 'kích hoạt';
    this.confirmDialogTitle = `${user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'} người dùng`;
    this.confirmDialogMessage = `Bạn có chắc muốn ${action} người dùng "${user.fullName}"?`;
    this.confirmDialogConfirmLabel = user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt';
    this.pendingUserAction = { user, activate: !user.isActive };
    this.showConfirmDialog = true;
    this.cdr.markForCheck();
  }

  onConfirmDialogConfirmed(): void {
    this.showConfirmDialog = false;
    const pending = this.pendingUserAction;
    this.pendingUserAction = null;
    if (!pending) return;

    const action = pending.user.isActive ? 'vô hiệu hóa' : 'kích hoạt';
    const obs$ = pending.user.isActive
      ? this.adminService.deactivateUser(pending.user.id)
      : this.adminService.activateUser(pending.user.id);

    obs$.pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.snackBar.open(`Đã ${action} người dùng thành công`, 'Đóng', { duration: 3000 });
          this.loadUsers();
        },
        error: () => {
          this.snackBar.open(`Không thể ${action} người dùng`, 'Đóng', { duration: 5000 });
        },
      });
  }

  onConfirmDialogCancelled(): void {
    this.showConfirmDialog = false;
    this.pendingUserAction = null;
  }
}
