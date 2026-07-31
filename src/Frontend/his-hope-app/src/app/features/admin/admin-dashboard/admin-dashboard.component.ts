import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { AdminService } from '@core/services/admin.service';
import { AdminDashboardStats } from '@core/models/admin.model';
import {
  HisHopeMetricCardComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule, MatButtonModule, MatIconModule, RouterModule,
    HisHopeMetricCardComponent, HisHopePageHeaderComponent, HisHopePageLayoutComponent,
    HisHopeStateComponent, HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header hhPageHeader [title]="'admin.dashboardTitle' | hhTranslate:'Quản trị hệ thống'"
                      [subtitle]="'Tổng quan về hệ thống và điều hướng nhanh' | hhTranslate" />

      @if (loading) {
        <hh-state kind="loading" [message]="'Đang tải dữ liệu...' | hhTranslate" />
      } @else if (error) {
        <hh-state kind="error" [message]="error">
          <button mat-stroked-button type="button" (click)="loadStats()">{{ 'common.retry' | hhTranslate }}</button>
        </hh-state>
      } @else if (stats) {
        <div class="stats-grid">
          <hh-metric-card icon="people" [label]="'Người dùng' | hhTranslate" [value]="stats.totalUsers"
                          link="/admin/manage-users" [action]="'Quản lý người dùng' | hhTranslate" />
          <hh-metric-card icon="admin_panel_settings" [label]="'Vai trò hoạt động' | hhTranslate" [value]="stats.activeRoles"
                          link="/admin/manage-roles" [action]="'Quản lý vai trò' | hhTranslate" tone="info" />
          <hh-metric-card icon="receipt_long" [label]="'Nhật ký gần nhất' | hhTranslate" [value]="lastAuditDate"
                          link="/admin/audit-logs" [action]="'Xem nhật ký' | hhTranslate" tone="warning" />
          <hh-metric-card [icon]="healthIcon" [label]="'Trạng thái hệ thống' | hhTranslate" [value]="healthLabel"
                          link="/admin/settings" [action]="'Xem cài đặt' | hhTranslate" [tone]="healthTone" />
        </div>

        <div class="quick-links">
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
      }
    </hh-page-layout>
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 16px; margin-bottom: 36px; }

    .quick-links h2 { font-size: 18px; font-weight: 700; color: var(--text-primary); margin: 0 0 16px; }
    .links-grid { display: flex; flex-wrap: wrap; gap: 12px; }
    .links-grid a { display: flex; align-items: center; gap: 8px; min-width: 180px; border-radius: 6px; border: 1px solid var(--border-default); }
    .links-grid a:hover { border-color: var(--mat-sys-primary); background: rgba(47, 107, 74, 0.03); }
    @media (max-width: 900px) { .stats-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 600px) { .stats-grid { grid-template-columns: 1fr; } }
  `],
})
export class AdminDashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  loading = true;
  error: string | null = null;
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
    this.error = null;
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
          this.error = 'Không thể tải dữ liệu tổng quan';
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

  get healthTone(): string {
    switch (this.stats?.systemHealth) {
      case 'healthy': return 'success';
      case 'degraded': return 'warning';
      case 'down': return 'danger';
      default: return 'neutral';
    }
  }
}
