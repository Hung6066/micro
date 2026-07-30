import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatMenuModule } from '@angular/material/menu';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { AdminUser } from '@core/models/admin.model';
import { PagedResult } from '@core/models/paged-result.model';
import { UserFormDialogComponent, UserFormData } from './user-form.dialog';
import { AssignRolesDialogComponent, AssignRolesData } from './assign-roles.dialog';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';
import { HisHopeConfirmDialogComponent } from '@his-hope/frontend-foundation';

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
    MatTableModule, MatPaginatorModule, MatButtonModule, MatIconModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatMenuModule,
    MatChipsModule, MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
    MatSnackBarModule,
    LoadingSpinnerComponent, EmptyStateComponent, HisHopeConfirmDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './manage-users.component.html',
  styleUrls: ['./manage-users.component.scss'],
})
export class ManageUsersComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  users: AdminUser[] = [];
  totalCount = 0;
  currentPage = 1;
  pageSize = 10;
  loading = true;

  searchControl = new FormControl('');
  roleFilter = new FormControl('');

  displayedColumns = ['id', 'fullName', 'roles', 'status', 'actions'];
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
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.currentPage = 1;
        this.loadUsers();
      });
    this.loadUsers();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadUsers(): void {
    this.loading = true;
    this.cdr.markForCheck();

    this.adminService.getUsers({
      search: this.searchControl.value || '',
      role: this.roleFilter.value || '',
      page: this.currentPage,
      pageSize: this.pageSize,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PagedResult<AdminUser>) => {
          this.users = result.items;
          this.totalCount = result.totalCount;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.snackBar.open('Không thể tải danh sách người dùng', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadUsers();
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadUsers();
  }

  getRoleLabel(role: string): string {
    const found = ROLE_FILTERS.find((f) => f.value === role);
    return found ? found.label : role;
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
