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
import { ConfirmDialogComponent, ConfirmDialogData } from '@shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

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
  standalone: false,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="manage-users">
      <div class="page-header">
        <div>
          <h1>Quản lý người dùng</h1>
          <p class="subtitle">Quản lý tài khoản bác sĩ, điều dưỡng và nhân viên</p>
        </div>
        <button mat-raised-button color="primary" (click)="openAddUserDialog()">
          <mat-icon>person_add</mat-icon>
          Thêm người dùng
        </button>
      </div>

      <div class="filter-bar">
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="search-field">
          <mat-icon matPrefix>search</mat-icon>
          <input matInput [formControl]="searchControl" placeholder="Tìm kiếm theo tên, email, ID...">
        </mat-form-field>
        <mat-form-field appearance="outline" subscriptSizing="dynamic" class="role-filter">
          <mat-select [formControl]="roleFilter" (selectionChange)="onFilterChange()">
            <mat-option *ngFor="let f of roleFilters" [value]="f.value">{{ f.label }}</mat-option>
          </mat-select>
        </mat-form-field>
      </div>

      <div class="table-container mat-elevation-z2">
        <app-loading-spinner [loading]="loading" message="Đang tải danh sách người dùng..."></app-loading-spinner>

        <ng-container *ngIf="!loading">
          <mat-table [dataSource]="users" class="users-table" *ngIf="users.length > 0; else noData">
            <ng-container matColumnDef="id">
              <mat-header-cell *matHeaderCellDef>ID</mat-header-cell>
              <mat-cell *matCellDef="let u" class="cell-id">{{ u.id | slice:0:12 }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="fullName">
              <mat-header-cell *matHeaderCellDef>Họ và tên</mat-header-cell>
              <mat-cell *matCellDef="let u">
                <div class="user-name-cell">
                  <span class="name">{{ u.fullName }}</span>
                  <span class="email">{{ u.email }}</span>
                </div>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="email">
              <mat-header-cell *matHeaderCellDef>Email</mat-header-cell>
              <mat-cell *matCellDef="let u">{{ u.email }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="roles">
              <mat-header-cell *matHeaderCellDef>Vai trò</mat-header-cell>
              <mat-cell *matCellDef="let u">
                <div class="roles-cell">
                  <span class="role-badge" *ngFor="let r of u.roles" [ngClass]="'role-' + r">{{ getRoleLabel(r) }}</span>
                </div>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="status">
              <mat-header-cell *matHeaderCellDef>Trạng thái</mat-header-cell>
              <mat-cell *matCellDef="let u">
                <span class="status-badge" [ngClass]="u.isActive ? 'active' : 'inactive'">
                  {{ u.isActive ? 'Hoạt động' : 'Ngưng hoạt động' }}
                </span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="actions">
              <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
              <mat-cell *matCellDef="let u">
                <button mat-icon-button color="primary" (click)="openEditUserDialog(u)"
                        matTooltip="Chỉnh sửa người dùng">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button (click)="openAssignRolesDialog(u)"
                        matTooltip="Phân vai trò">
                  <mat-icon>admin_panel_settings</mat-icon>
                </button>
                <button mat-icon-button (click)="toggleUserStatus(u)"
                        [matTooltip]="u.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'">
                  <mat-icon [color]="u.isActive ? 'warn' : 'primary'">
                    {{ u.isActive ? 'block' : 'check_circle' }}
                  </mat-icon>
                </button>
              </mat-cell>
            </ng-container>

            <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
            <mat-row *matRowDef="let row; columns: displayedColumns;"></mat-row>
          </mat-table>

          <ng-template #noData>
            <app-empty-state icon="people" title="Không tìm thấy người dùng"
                            message="Thử thay đổi bộ lọc hoặc thêm người dùng mới">
            </app-empty-state>
          </ng-template>
        </ng-container>

        <mat-paginator *ngIf="totalCount > 0" [length]="totalCount" [pageSize]="pageSize"
                       [pageSizeOptions]="[5, 10, 20, 50]" [pageIndex]="currentPage - 1"
                       (page)="onPageChange($event)" showFirstLastButtons>
        </mat-paginator>
      </div>
    </div>
  `,
  styles: [`
    .manage-users { padding: 24px; max-width: 1200px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 600; color: #1A1A1A; }
    .subtitle { margin: 4px 0 0; color: #787774; font-size: 14px; }

    .filter-bar { display: flex; gap: 12px; margin-bottom: 20px; align-items: flex-start; }
    .search-field { flex: 1; max-width: 400px; }
    .role-filter { width: 200px; }

    .table-container { background: #FFFFFF; border-radius: 8px; border: 1px solid #EAEAEA; overflow: hidden; }
    .users-table { width: 100%; }
    .cell-id { font-family: 'Roboto Mono', monospace; font-size: 12px; color: #787774; }

    .user-name-cell { display: flex; flex-direction: column; }
    .user-name-cell .name { font-weight: 500; color: #1A1A1A; }
    .user-name-cell .email { font-size: 12px; color: #787774; }

    .roles-cell { display: flex; flex-wrap: wrap; gap: 4px; }
    .role-badge { display: inline-flex; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 500; text-transform: uppercase; letter-spacing: 0.3px; }
    .role-admin { background: #e8f5e9; color: #2F6B4A; }
    .role-doctor { background: #e3f2fd; color: #1565c0; }
    .role-nurse { background: #fce4ec; color: #c62828; }
    .role-pharmacist { background: #fff3e0; color: #e65100; }
    .role-receptionist { background: #f3e5f5; color: #7b1fa2; }
    .role-manager { background: #e0f2f1; color: #00695c; }

    .status-badge { display: inline-flex; padding: 2px 10px; border-radius: 12px; font-size: 12px; font-weight: 500; }
    .status-badge.active { background: #e8f5e9; color: #2F6B4A; }
    .status-badge.inactive { background: #fbe9e7; color: #c62828; }

    mat-row:hover { background: #F7F6F3; }
    .mat-mdc-header-cell { font-weight: 600; color: #787774; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; }
  `],
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
    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData>(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: `${user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt'} người dùng`,
        message: `Bạn có chắc muốn ${action} người dùng "${user.fullName}"?`,
        confirmLabel: user.isActive ? 'Vô hiệu hóa' : 'Kích hoạt',
        cancelLabel: 'Hủy',
        confirmColor: user.isActive ? 'warn' : 'primary',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((confirmed) => {
        if (!confirmed) return;
        const obs$ = user.isActive
          ? this.adminService.deactivateUser(user.id)
          : this.adminService.activateUser(user.id);

        obs$.pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open(
                `Đã ${action} người dùng thành công`, 'Đóng', { duration: 3000 },
              );
              this.loadUsers();
            },
            error: () => {
              this.snackBar.open(
                `Không thể ${action} người dùng`, 'Đóng', { duration: 5000 },
              );
            },
          });
      });
  }
}
