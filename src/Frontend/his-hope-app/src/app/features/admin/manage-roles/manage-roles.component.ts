import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { Subject, takeUntil } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { AdminService } from '@core/services/admin.service';
import { Role } from '@core/models/admin.model';
import { RoleFormDialogComponent, RoleFormData } from './role-form.dialog';
import { ConfirmDialogComponent, ConfirmDialogData } from '@shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '@shared/components/empty-state/empty-state.component';

@Component({
  selector: 'app-manage-roles',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule, MatButtonModule, MatIconModule, MatMenuModule,
    MatProgressSpinnerModule, MatTooltipModule, MatDialogModule,
    MatSnackBarModule,
    RoleFormDialogComponent,
    ConfirmDialogComponent, LoadingSpinnerComponent, EmptyStateComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="manage-roles">
      <div class="page-header">
        <div>
          <h1>Quản lý vai trò & quyền</h1>
          <p class="subtitle">Cấu hình kiểm soát truy cập dựa trên vai trò (RBAC)</p>
        </div>
        <button mat-raised-button color="primary" (click)="openAddRoleDialog()">
          <mat-icon>add_moderator</mat-icon>
          Thêm vai trò
        </button>
      </div>

      <div class="table-container mat-elevation-z2">
        <app-loading-spinner [loading]="loading" message="Đang tải danh sách vai trò..."></app-loading-spinner>

        <ng-container *ngIf="!loading">
          <mat-table [dataSource]="roles" class="roles-table" *ngIf="roles.length > 0; else noData">
            <ng-container matColumnDef="name">
              <mat-header-cell *matHeaderCellDef>Tên vai trò</mat-header-cell>
              <mat-cell *matCellDef="let r">
                <div class="role-name-cell">
                  <span class="name">{{ getRoleLabel(r.name) }}</span>
                  <span class="desc">{{ r.description }}</span>
                </div>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="usersCount">
              <mat-header-cell *matHeaderCellDef>Người dùng</mat-header-cell>
              <mat-cell *matCellDef="let r">{{ r.usersCount }}</mat-cell>
            </ng-container>

            <ng-container matColumnDef="system">
              <mat-header-cell *matHeaderCellDef>Hệ thống</mat-header-cell>
              <mat-cell *matCellDef="let r">
                <span class="system-badge" [ngClass]="r.isSystem ? 'system' : 'custom'">
                  {{ r.isSystem ? 'Hệ thống' : 'Tùy chỉnh' }}
                </span>
              </mat-cell>
            </ng-container>

            <ng-container matColumnDef="permissions">
              <mat-header-cell *matHeaderCellDef>Quyền</mat-header-cell>
              <mat-cell *matCellDef="let r">{{ r.permissions.length }} quyền</mat-cell>
            </ng-container>

            <ng-container matColumnDef="actions">
              <mat-header-cell *matHeaderCellDef>Thao tác</mat-header-cell>
              <mat-cell *matCellDef="let r">
                <button mat-icon-button color="primary" (click)="openEditRoleDialog(r)"
                        matTooltip="Chỉnh sửa vai trò">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button color="warn" (click)="deleteRole(r)"
                        [disabled]="r.isSystem"
                        matTooltip="{{ r.isSystem ? 'Không thể xóa vai trò hệ thống' : 'Xóa vai trò' }}">
                  <mat-icon>delete</mat-icon>
                </button>
              </mat-cell>
            </ng-container>

            <mat-header-row *matHeaderRowDef="displayedColumns"></mat-header-row>
            <mat-row *matRowDef="let row; columns: displayedColumns;" class="clickable-row"
                     (click)="openEditRoleDialog(row)"></mat-row>
          </mat-table>

          <ng-template #noData>
            <app-empty-state icon="admin_panel_settings" title="Không có vai trò nào"
                            message="Thêm vai trò mới để bắt đầu cấu hình quyền">
            </app-empty-state>
          </ng-template>
        </ng-container>
      </div>
    </div>
  `,
  styles: [`
    .manage-roles { padding: 24px; max-width: 1000px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 600; color: #1A1A1A; }
    .subtitle { margin: 4px 0 0; color: #787774; font-size: 14px; }

    .table-container { background: #FFFFFF; border-radius: 8px; border: 1px solid #EAEAEA; overflow: hidden; }
    .roles-table { width: 100%; }

    .role-name-cell { display: flex; flex-direction: column; }
    .role-name-cell .name { font-weight: 500; color: #1A1A1A; text-transform: capitalize; }
    .role-name-cell .desc { font-size: 12px; color: #787774; margin-top: 2px; max-width: 300px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

    .system-badge { display: inline-flex; padding: 2px 10px; border-radius: 12px; font-size: 12px; font-weight: 500; }
    .system-badge.system { background: #e8f5e9; color: #2F6B4A; }
    .system-badge.custom { background: #e3f2fd; color: #1565c0; }

    .clickable-row { cursor: pointer; }
    .clickable-row:hover { background: #F7F6F3; }
    .mat-mdc-header-cell { font-weight: 600; color: #787774; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px; }
  `],
})
export class ManageRolesComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  roles: Role[] = [];
  loading = true;

  displayedColumns = ['name', 'usersCount', 'system', 'permissions', 'actions'];

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
    this.cdr.markForCheck();

    this.adminService.getRoles()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (roles) => {
          this.roles = roles;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
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

    const dialogRef = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData>(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Xóa vai trò',
        message: `Bạn có chắc muốn xóa vai trò "${this.getRoleLabel(role.name)}"? Hành động này không thể hoàn tác.`,
        confirmLabel: 'Xóa',
        cancelLabel: 'Hủy',
        confirmColor: 'warn',
      },
    });

    dialogRef.afterClosed()
      .pipe(takeUntil(this.destroy$))
      .subscribe((confirmed) => {
        if (!confirmed) return;
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
      });
  }
}
