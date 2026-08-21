import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
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
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation/i18n';

const SEVERITY_CONFIG: Record<
  string,
  { dotColor: string; bg: string; label: string }
> = {
  critical: { dotColor: '#C25450', bg: '#FDEBEC', label: 'Nghiêm trọng' },
  warning: { dotColor: '#B6581C', bg: '#FDF0E2', label: 'Cảnh báo' },
  info: { dotColor: '#1F6C9F', bg: '#E1F3FE', label: 'Thông tin' },
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
    HisHopeTranslatePipe,
  ],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="alertMenu"
      [matBadge]="totalAlerts$ | async"
      matBadgeColor="warn"
      matBadgeSize="small"
      [attr.aria-label]="
        'dashboard.alerts.alertNotification' | hhTranslate: 'Thông báo cảnh báo'
      "
      class="alert-bell-btn"
    >
      <mat-icon>notifications</mat-icon>
    </button>

    <mat-menu
      #alertMenu="matMenu"
      class="alert-menu-panel"
      xPosition="before"
      yPosition="below"
    >
      <div class="alert-menu-header">
        <span class="alert-menu-title">{{
          'dashboard.alerts.systemAlerts' | hhTranslate: 'Cảnh báo hệ thống'
        }}</span>
        @let total = totalAlerts$ | async;
        @if (total !== null) {
          <span class="alert-menu-count"
            >{{ total }}
            {{
              'dashboard.alerts.active' | hhTranslate: 'đang hoạt động'
            }}</span
          >
        }
      </div>

      @let alerts = activeAlerts$ | async;
      @if (alerts) {
        @if (alerts.length > 0) {
          <div class="alert-menu-body">
            @for (alert of alerts; track alert.summary + alert.service) {
              <button
                mat-menu-item
                class="alert-menu-item"
                [style.--alert-dot-color]="
                  getSeverityConfig(alert.severity).dotColor
                "
                [style.--alert-bg]="getSeverityConfig(alert.severity).bg"
              >
                <div class="alert-item">
                  <span
                    class="alert-severity-dot"
                    [style.background]="
                      getSeverityConfig(alert.severity).dotColor
                    "
                  ></span>
                  <div class="alert-content">
                    <div class="alert-heading">
                      <span class="alert-service">{{ alert.service }}</span>
                      <span class="alert-time">{{
                        alert.startsAt | relativeTime
                      }}</span>
                    </div>
                    <div class="alert-summary">{{ alert.summary }}</div>
                  </div>
                </div>
              </button>
            }
          </div>
        } @else {
          <div class="alert-empty">
            <mat-icon class="alert-empty-icon">check_circle</mat-icon>
            <span>{{
              'dashboard.alerts.noAlerts' | hhTranslate: 'Không có cảnh báo nào'
            }}</span>
          </div>
        }
      }
    </mat-menu>
  `,
  styles: [
    `
      :host {
        display: inline-flex;
        align-items: center;
      }

      .alert-bell-btn {
        position: relative;
      }

      /* ── Menu Panel ── */
      :host ::ng-deep .alert-menu-panel {
        min-width: 360px;
        max-width: 420px;
        border-radius: var(--radius-card);
        border: 1px solid var(--border-default, #eaeaea);
        background: var(--surface-white, #ffffff);
        margin-top: var(--space-2xs);
      }

      .alert-menu-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: var(--space-lg) var(--space-lg) var(--space-md);
        border-bottom: 1px solid var(--border-default, #eaeaea);
      }

      .alert-menu-title {
        font-size: var(--font-size-body);
        font-weight: 600;
        color: var(--text-primary, #1a1a1a);
      }

      .alert-menu-count {
        font-size: var(--font-size-nav);
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
        min-height: var(--mobile-toolbar-height);
        padding: var(--space-sm) var(--space-lg) !important;
        border-bottom: 1px solid var(--border-light, #f0f0ee);
        line-height: 1.4;
        transition: background-color 150ms ease;
      }

      .alert-menu-item:last-child {
        border-bottom: none;
      }

      .alert-item {
        display: flex;
        align-items: flex-start;
        gap: var(--space-inset);
        width: 100%;
      }

      .alert-severity-dot {
        display: inline-block;
        width: var(--size-status-dot);
        height: var(--size-status-dot);
        border-radius: 50%;
        flex-shrink: 0;
        margin-top: var(--space-snug);
      }

      .alert-content {
        flex: 1;
        min-width: 0;
      }

      .alert-heading {
        display: flex;
        justify-content: space-between;
        align-items: center;
        gap: var(--space-sm);
        margin-bottom: var(--space-hairline);
      }

      .alert-service {
        font-size: var(--font-size-caption);
        font-weight: 600;
        color: var(--text-primary, #1a1a1a);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .alert-time {
        font-size: var(--font-size-nav);
        color: var(--text-secondary, #787774);
        white-space: nowrap;
        flex-shrink: 0;
      }

      .alert-summary {
        font-size: var(--font-size-caption);
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
        padding: var(--space-3xl) var(--space-lg);
        gap: var(--space-sm);
        color: var(--text-muted, #a1a09b);
      }

      .alert-empty-icon {
        font-size: var(--font-size-display-md);
        width: var(--space-3xl);
        height: var(--space-3xl);
        opacity: 0.5;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertPanelComponent {
  readonly activeAlerts$: Observable<Alert[]>;
  readonly totalAlerts$: Observable<number>;

  private readonly alertService = inject(AlertService);
  private readonly i18n = inject(HisHopeI18nService);

  constructor() {
    this.activeAlerts$ = this.alertService.activeAlerts$;
    this.totalAlerts$ = this.activeAlerts$.pipe(
      map((alerts) => alerts.filter((a) => a.status === 'firing').length),
    );
  }

  getSeverityConfig(severity: string): {
    dotColor: string;
    bg: string;
    label: string;
  } {
    const config = SEVERITY_CONFIG[severity] ?? SEVERITY_CONFIG['info'];
    return {
      ...config,
      label: this.i18n.t(`dashboard.alerts.severity.${severity}`, config.label),
    };
  }
}
