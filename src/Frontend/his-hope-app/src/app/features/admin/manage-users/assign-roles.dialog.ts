import { Component, Inject, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
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

export interface AssignRolesData {
  user: AdminUser;
}

@Component({
  selector: 'app-assign-roles-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatCheckboxModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Phân vai trò</h2>
    <mat-dialog-content>
      <div class="user-info">
        <mat-icon>person</mat-icon>
        <span>{{ data.user.fullName }}</span>
      </div>
      <p class="instruction">Chọn các vai trò cho người dùng:</p>

      <div class="roles-list" *ngIf="roles.length > 0; else loadingRoles">
        <div class="role-item" *ngFor="let role of roles; let i = index">
          <mat-checkbox [formControl]="roleCheckboxes.at(i)!">
            <div class="role-info">
              <span class="role-name">{{ role.name }}</span>
              <span class="role-desc">{{ role.description }}</span>
            </div>
          </mat-checkbox>
        </div>
      </div>
      <ng-template #loadingRoles>
        <div class="loading-state">
          <mat-spinner diameter="24"></mat-spinner>
          <span>Đang tải vai trò...</span>
        </div>
      </ng-template>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="saving">Hủy</button>
      <button mat-raised-button color="primary" (click)="save()" [disabled]="saving">
        <mat-icon>save</mat-icon>
        <span *ngIf="!saving">Lưu vai trò</span>
        <mat-spinner *ngIf="saving" diameter="20"></mat-spinner>
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .user-info { display: flex; align-items: center; gap: 8px; margin-bottom: 16px; padding: 8px 12px; background: #e8f5e9; border-radius: 8px; color: #2F6B4A; font-weight: 500; }
    .instruction { color: #666; font-size: 14px; margin-bottom: 16px; }
    .roles-list { display: flex; flex-direction: column; gap: 8px; min-width: 400px; max-height: 400px; overflow-y: auto; }
    .role-item { padding: 8px 12px; border: 1px solid #EAEAEA; border-radius: 8px; transition: background 0.15s; }
    .role-item:hover { background: #f9f9f7; }
    .role-info { display: flex; flex-direction: column; }
    .role-name { font-weight: 500; font-size: 14px; color: #1A1A1A; }
    .role-desc { font-size: 12px; color: #787774; margin-top: 2px; }
    .loading-state { display: flex; align-items: center; gap: 12px; justify-content: center; padding: 48px; color: #787774; }
  `],
})
export class AssignRolesDialogComponent implements OnDestroy {
  private destroy$ = new Subject<void>();

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
          this.snackBar.open('Đã cập nhật vai trò thành công', 'Đóng', { duration: 3000 });
          this.dialogRef.close(true);
        },
        error: () => {
          this.saving = false;
          this.snackBar.open('Không thể cập nhật vai trò', 'Đóng', { duration: 5000 });
          this.cdr.markForCheck();
        },
      });
  }
}
