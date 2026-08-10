import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef, inject } from '@angular/core';
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
  HisHopeI18nService,
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
      <hh-page-header hhPageHeader [title]="'adminPage.dashboardTitle' | hhTranslate:'Quản trị hệ thống'"
                      [subtitle]="'adminPage.dashboardSubtitle' | hhTranslate:'Tổng quan về hệ thống và điều hướng nhanh'" />

      @if (loading) {
        <hh-state kind="loading" [message]="'common.loading' | hhTranslate:'Đang tải dữ liệu...'" />
      } @else if (error) {
        <hh-state kind="error" [message]="error">
          <button mat-stroked-button type="button" (click)="loadStats()">{{ 'common.retry' | hhTranslate }}</button>
        </hh-state>
      } @else if (stats) {
        <div class="stats-grid">
          <hh-metric-card icon="people" [label]="'adminPage.totalUsers' | hhTranslate:'Người dùng'" [value]="stats.totalUsers"
                          link="/admin/manage-users" [action]="'adminPage.manageUsers' | hhTranslate:'Quản lý người dùng'" />
          <hh-metric-card icon="admin_panel_settings" [label]="'adminPage.activeRolesLabel' | hhTranslate:'Vai trò hoạt động'" [value]="stats.activeRoles"
                          link="/admin/manage-roles" [action]="'adminPage.manageRoles' | hhTranslate:'Quản lý vai trò'" tone="info" />
          <hh-metric-card icon="receipt_long" [label]="'adminPage.recentLogs' | hhTranslate:'Nhật ký gần nhất'" [value]="lastAuditDate"
                          link="/admin/audit-logs" [action]="'adminPage.viewLogs' | hhTranslate:'Xem nhật ký'" tone="warning" />
          <hh-metric-card [icon]="healthIcon" [label]="'adminPage.systemHealth' | hhTranslate:'Trạng thái hệ thống'" [value]="healthLabel"
                          link="/admin/settings" [action]="'adminPage.viewSettings' | hhTranslate:'Xem cài đặt'" [tone]="healthTone" />
        </div>

        <div class="quick-links">
          <h2>{{ 'adminPage.quickAccess' | hhTranslate:'Truy cập nhanh' }}</h2>
          <div class="links-grid">
            <a mat-stroked-button routerLink="/admin/manage-users">
              <mat-icon>people</mat-icon> {{ 'adminPage.manageUsers' | hhTranslate:'Quản lý người dùng' }}
            </a>
            <a mat-stroked-button routerLink="/admin/manage-roles">
              <mat-icon>admin_panel_settings</mat-icon> {{ 'adminPage.rolesAndPermissions' | hhTranslate:'Vai trò & quyền' }}
            </a>
            <a mat-stroked-button routerLink="/admin/settings">
              <mat-icon>settings</mat-icon> {{ 'adminPage.settings' | hhTranslate:'Cài đặt hệ thống' }}
            </a>
            <a mat-stroked-button routerLink="/admin/audit-logs">
              <mat-icon>receipt_long</mat-icon> {{ 'adminPage.auditLogs' | hhTranslate:'Nhật ký truy cập' }}
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
  private i18n = inject(HisHopeI18nService);

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
          this.error = this.i18n.t('adminPage.loadError', 'Không thể tải dữ liệu tổng quan');
          this.cdr.markForCheck();
        },
      });
  }

  get lastAuditDate(): string {
    if (!this.stats?.lastAuditEntry) return this.i18n.t('adminPage.noData', 'Chưa có');
    const d = new Date(this.stats.lastAuditEntry);
    const now = new Date();
    const diffHours = Math.floor((now.getTime() - d.getTime()) / 3600000);
    if (diffHours < 1) return this.i18n.t('adminPage.justNow', 'Vừa xong');
    if (diffHours < 24) return this.i18n.t('adminPage.hoursAgo', '{{n}} giờ trước', { n: diffHours });
    const diffDays = Math.floor(diffHours / 24);
    return this.i18n.t('adminPage.daysAgo', '{{n}} ngày trước', { n: diffDays });
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
      case 'healthy': return this.i18n.t('adminPage.healthHealthy', 'Hoạt động tốt');
      case 'degraded': return this.i18n.t('adminPage.healthDegraded', 'Suy giảm');
      case 'down': return this.i18n.t('adminPage.healthDown', 'Ngừng hoạt động');
      default: return this.i18n.t('adminPage.healthUnknown', 'Không xác định');
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
