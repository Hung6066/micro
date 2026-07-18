import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AdminService } from '@core/services/admin.service';
import { AdminDashboardStats } from '@core/models/admin.model';
import { LoadingSpinnerComponent } from '@shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule, RouterModule, LoadingSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="admin-dashboard">
      <div class="page-header">
        <h1>Quản trị hệ thống</h1>
        <p class="subtitle">Tổng quan về hệ thống và điều hướng nhanh</p>
      </div>

      <app-loading-spinner [loading]="loading" message="Đang tải dữ liệu..."></app-loading-spinner>

      <div class="stats-grid" *ngIf="!loading">
        <mat-card class="stat-card" routerLink="/admin/manage-users">
          <mat-card-content>
            <div class="stat-icon users">
              <mat-icon>people</mat-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ stats?.totalUsers ?? 0 }}</span>
              <span class="stat-label">Người dùng</span>
            </div>
          </mat-card-content>
          <mat-card-actions>
            <button mat-button color="primary">Quản lý người dùng</button>
          </mat-card-actions>
        </mat-card>

        <mat-card class="stat-card" routerLink="/admin/manage-roles">
          <mat-card-content>
            <div class="stat-icon roles">
              <mat-icon>admin_panel_settings</mat-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ stats?.activeRoles ?? 0 }}</span>
              <span class="stat-label">Vai trò hoạt động</span>
            </div>
          </mat-card-content>
          <mat-card-actions>
            <button mat-button color="primary">Quản lý vai trò</button>
          </mat-card-actions>
        </mat-card>

        <mat-card class="stat-card" routerLink="/admin/audit-logs">
          <mat-card-content>
            <div class="stat-icon audit">
              <mat-icon>receipt_long</mat-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ lastAuditDate }}</span>
              <span class="stat-label">Nhật ký gần nhất</span>
            </div>
          </mat-card-content>
          <mat-card-actions>
            <button mat-button color="primary">Xem nhật ký</button>
          </mat-card-actions>
        </mat-card>

        <mat-card class="stat-card" routerLink="/admin/settings">
          <mat-card-content>
            <div class="stat-icon" [ngClass]="healthClass">
              <mat-icon>{{ healthIcon }}</mat-icon>
            </div>
            <div class="stat-info">
              <span class="stat-value">{{ healthLabel }}</span>
              <span class="stat-label">Trạng thái hệ thống</span>
            </div>
          </mat-card-content>
          <mat-card-actions>
            <button mat-button color="primary">Xem cài đặt</button>
          </mat-card-actions>
        </mat-card>
      </div>

      <div class="quick-links" *ngIf="!loading">
        <h2>Truy cập nhanh</h2>
        <div class="links-grid">
          <a mat-stroked-button routerLink="/admin/manage-users">
            <mat-icon>people</mat-icon> Quản lý người dùng
          </a>
          <a mat-stroked-button routerLink="/admin/manage-roles">
            <mat-icon>admin_panel_settings</mat-icon> Vai trò & quyền
          </a>
          <a mat-stroked-button routerLink="/admin/settings">
            <mat-icon>settings</mat-icon> Cài đặt hệ thống
          </a>
          <a mat-stroked-button routerLink="/admin/audit-logs">
            <mat-icon>receipt_long</mat-icon> Nhật ký truy cập
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .admin-dashboard { padding: 24px; max-width: 1000px; margin: 0 auto; }
    .page-header { margin-bottom: 28px; }
    .page-header h1 { margin: 0; font-size: 24px; font-weight: 600; color: #1A1A1A; }
    .subtitle { margin: 4px 0 0; color: #787774; font-size: 14px; }

    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 20px; margin-bottom: 32px; }
    .stat-card { border-radius: 8px; border: 1px solid #EAEAEA; cursor: pointer; transition: border-color 0.15s, box-shadow 0.15s; }
    .stat-card:hover { border-color: #2F6B4A; box-shadow: 0 1px 4px rgba(47, 107, 74, 0.1); }
    .stat-card:active { transform: scale(0.98); }
    .stat-card mat-card-content { display: flex; align-items: center; gap: 16px; padding: 20px; }
    .stat-icon { display: flex; align-items: center; justify-content: center; width: 48px; height: 48px; border-radius: 12px; flex-shrink: 0; }
    .stat-icon mat-icon { font-size: 24px; width: 24px; height: 24px; }
    .stat-icon.users { background: #e8f5e9; color: #2F6B4A; }
    .stat-icon.roles { background: #e3f2fd; color: #1565c0; }
    .stat-icon.audit { background: #fff3e0; color: #e65100; }
    .stat-icon.healthy { background: #e8f5e9; color: #2F6B4A; }
    .stat-icon.degraded { background: #fff3e0; color: #e65100; }
    .stat-icon.down { background: #fbe9e7; color: #c62828; }
    .stat-info { display: flex; flex-direction: column; }
    .stat-value { font-size: 22px; font-weight: 700; color: #1A1A1A; line-height: 1.2; }
    .stat-label { font-size: 12px; color: #787774; margin-top: 2px; }
    mat-card-actions { padding: 0 16px 12px; }

    .quick-links h2 { font-size: 18px; font-weight: 600; color: #1A1A1A; margin: 0 0 16px; }
    .links-grid { display: flex; flex-wrap: wrap; gap: 12px; }
    .links-grid a { display: flex; align-items: center; gap: 8px; min-width: 180px; border-radius: 6px; border: 1px solid #EAEAEA; }
    .links-grid a:hover { border-color: #2F6B4A; background: rgba(47, 107, 74, 0.03); }
  `],
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  loading = true;
  stats: AdminDashboardStats | null = null;

  constructor(
    private adminService: AdminService,
    private cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.loadStats();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadStats(): void {
    this.loading = true;
    this.cdr.markForCheck();

    this.adminService.getDashboardStats()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (stats) => {
          this.stats = stats;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.loading = false;
          this.cdr.markForCheck();
        },
      });
  }

  get lastAuditDate(): string {
    if (!this.stats?.lastAuditEntry) return 'Chưa có';
    const d = new Date(this.stats.lastAuditEntry);
    const now = new Date();
    const diffHours = Math.floor((now.getTime() - d.getTime()) / 3600000);
    if (diffHours < 1) return 'Vừa xong';
    if (diffHours < 24) return `${diffHours} giờ trước`;
    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays} ngày trước`;
  }

  get healthClass(): string {
    return this.stats?.systemHealth || 'healthy';
  }

  get healthIcon(): string {
    switch (this.stats?.systemHealth) {
      case 'healthy': return 'check_circle';
      case 'degraded': return 'warning';
      case 'down': return 'error';
      default: return 'help';
    }
  }

  get healthLabel(): string {
    switch (this.stats?.systemHealth) {
      case 'healthy': return 'Hoạt động tốt';
      case 'degraded': return 'Suy giảm';
      case 'down': return 'Ngừng hoạt động';
      default: return 'Không xác định';
    }
  }
}
