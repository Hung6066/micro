import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatBadgeModule } from '@angular/material/badge';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AlertService } from '../../core/services/alert.service';
import { Alert } from '../../core/models/alert.model';
import { RelativeTimePipe } from '../pipes/relative-time.pipe';

const SEVERITY_CONFIG: Record<string, { dotColor: string; bg: string; label: string }> = {
  critical: { dotColor: '#C25450', bg: '#FDEBEC', label: 'Nghiêm trọng' },
  warning:  { dotColor: '#B6581C', bg: '#FDF0E2', label: 'Cảnh báo' },
  info:     { dotColor: '#1F6C9F', bg: '#E1F3FE', label: 'Thông tin' },
};

@Component({
  selector: 'app-alert-panel',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatBadgeModule,
    RelativeTimePipe,
  ],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="alertMenu"
      [matBadge]="totalAlerts$ | async"
      matBadgeColor="warn"
      matBadgeSize="small"
      aria-label="Thông báo cảnh báo"
      class="alert-bell-btn"
    >
      <mat-icon>notifications</mat-icon>
    </button>

    <mat-menu #alertMenu="matMenu" class="alert-menu-panel" xPosition="before" yPosition="below">
      <div class="alert-menu-header">
        <span class="alert-menu-title">Cảnh báo hệ thống</span>
        <span class="alert-menu-count" *ngIf="(totalAlerts$ | async) as total">
          {{ total }} đang hoạt động
        </span>
      </div>

      <ng-container *ngIf="(activeAlerts$ | async) as alerts">
        <div class="alert-menu-body" *ngIf="alerts.length > 0; else emptyAlerts">
          <button
            *ngFor="let alert of alerts"
            mat-menu-item
            class="alert-menu-item"
            [style.--alert-dot-color]="getSeverityConfig(alert.severity).dotColor"
            [style.--alert-bg]="getSeverityConfig(alert.severity).bg"
          >
            <div class="alert-item">
              <span class="alert-severity-dot" [style.background]="getSeverityConfig(alert.severity).dotColor"></span>
              <div class="alert-content">
                <div class="alert-heading">
                  <span class="alert-service">{{ alert.service }}</span>
                  <span class="alert-time">{{ alert.startsAt | relativeTime }}</span>
                </div>
                <div class="alert-summary">{{ alert.summary }}</div>
              </div>
            </div>
          </button>
        </div>
      </ng-container>

      <ng-template #emptyAlerts>
        <div class="alert-empty">
          <mat-icon class="alert-empty-icon">check_circle</mat-icon>
          <span>Không có cảnh báo nào</span>
        </div>
      </ng-template>
    </mat-menu>
  `,
  styles: [`
    :host { display: inline-flex; align-items: center; }

    .alert-bell-btn {
      position: relative;
    }

    /* ── Menu Panel ── */
    :host ::ng-deep .alert-menu-panel {
      min-width: 360px;
      max-width: 420px;
      border-radius: 8px;
      border: 1px solid var(--border-default, #EAEAEA);
      background: var(--surface-white, #FFFFFF);
      margin-top: 4px;
    }

    .alert-menu-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 16px 12px;
      border-bottom: 1px solid var(--border-default, #EAEAEA);
    }

    .alert-menu-title {
      font-size: 14px;
      font-weight: 600;
      color: var(--text-primary, #1A1A1A);
    }

    .alert-menu-count {
      font-size: 11px;
      font-weight: 500;
      color: var(--text-secondary, #787774);
      letter-spacing: 0.02em;
    }

    .alert-menu-body {
      max-height: 320px;
      overflow-y: auto;
    }

    .alert-menu-item {
      height: auto !important;
      min-height: 56px;
      padding: 8px 16px !important;
      border-bottom: 1px solid var(--border-light, #F0F0EE);
      line-height: 1.4;
      transition: background-color 150ms ease;
    }

    .alert-menu-item:last-child {
      border-bottom: none;
    }

    .alert-item {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      width: 100%;
    }

    .alert-severity-dot {
      display: inline-block;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      flex-shrink: 0;
      margin-top: 5px;
    }

    .alert-content {
      flex: 1;
      min-width: 0;
    }

    .alert-heading {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 8px;
      margin-bottom: 2px;
    }

    .alert-service {
      font-size: 12px;
      font-weight: 600;
      color: var(--text-primary, #1A1A1A);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .alert-time {
      font-size: 11px;
      color: var(--text-secondary, #787774);
      white-space: nowrap;
      flex-shrink: 0;
    }

    .alert-summary {
      font-size: 12px;
      color: var(--text-secondary, #787774);
      line-height: 1.4;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .alert-empty {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 32px 16px;
      gap: 8px;
      color: var(--text-muted, #A1A09B);
    }

    .alert-empty-icon {
      font-size: 32px;
      width: 32px;
      height: 32px;
      opacity: 0.5;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertPanelComponent {
  readonly activeAlerts$: Observable<Alert[]>;
  readonly totalAlerts$: Observable<number>;

  constructor(private readonly alertService: AlertService) {
    this.activeAlerts$ = this.alertService.activeAlerts$;
    this.totalAlerts$ = this.activeAlerts$.pipe(
      map(alerts => alerts.filter(a => a.status === 'firing').length),
    );
  }

  getSeverityConfig(severity: string): { dotColor: string; bg: string; label: string } {
    return SEVERITY_CONFIG[severity] ?? SEVERITY_CONFIG['info'];
  }
}
