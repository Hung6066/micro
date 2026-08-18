import {
  Component,
  Inject,
  ChangeDetectionStrategy,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from '@his-hope/frontend-foundation/i18n';
import {
  HisHopeStatusBadgeComponent,
  HisHopeStatusTone,
} from '@his-hope/frontend-foundation/ui';
import { Resource, HealthCheckResult } from '../../core/models/resource.model';

@Component({
  selector: 'app-resource-detail',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatChipsModule,
    HisHopeStatusBadgeComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <div class="detail-overlay">
      <div class="detail-header">
        <div class="detail-header-left">
          <mat-icon class="detail-icon">dns</mat-icon>
          <div>
            <h2 class="detail-title">
              {{ resource.displayName || resource.name }}
            </h2>
            <span class="detail-subtitle">{{ resource.type }}</span>
          </div>
        </div>
        <hh-status-badge
          [status]="resource.status"
          [label]="statusLabel(resource.status)"
          [tone]="statusTone(resource.status)"
        />
      </div>

      <mat-divider></mat-divider>

      <!-- Basic Info -->
      <section class="detail-section">
        <h3 class="section-title">
          {{
            'dashboard.resources.basicInfo' | hhTranslate: 'Thông tin cơ bản'
          }}
        </h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">{{
              'dashboard.resources.name' | hhTranslate: 'Tên'
            }}</span>
            <span class="info-value">{{ resource.name }}</span>
          </div>
          @if (resource.version) {
            <div class="info-item">
              <span class="info-label">{{
                'dashboard.resources.version' | hhTranslate: 'Phiên bản'
              }}</span>
              <span class="info-value">{{ resource.version }}</span>
            </div>
          }
          <div class="info-item">
            <span class="info-label">{{
              'dashboard.resources.status' | hhTranslate: 'Trạng thái'
            }}</span>
            <span class="info-value">{{ resource.status }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">{{
              'dashboard.resources.health' | hhTranslate: 'Sức khỏe'
            }}</span>
            <span
              class="info-value"
              [class.text-green]="resource.healthStatus === 'Healthy'"
              [class.text-red]="resource.healthStatus === 'Unhealthy'"
            >
              {{ resource.healthStatus }}
            </span>
          </div>
        </div>
      </section>

      <mat-divider></mat-divider>

      <!-- Endpoints -->
      @if (endpoints.length > 0) {
        <section class="detail-section">
          <h3 class="section-title">
            {{ 'dashboard.resources.endpoints' | hhTranslate: 'Endpoints' }}
          </h3>
          <div class="endpoint-list">
            @for (ep of endpoints; track ep) {
              <div class="endpoint-item">
                <mat-icon>link</mat-icon>
                <code>{{ ep }}</code>
              </div>
            }
          </div>
        </section>
      }

      <mat-divider></mat-divider>

      <!-- Health Checks -->
      @if (healthChecks.length > 0) {
        <section class="detail-section">
          <h3 class="section-title">
            {{
              'dashboard.resources.healthChecks' | hhTranslate: 'Health Checks'
            }}
          </h3>
          <div class="health-list">
            @for (hc of healthChecks; track hc.description) {
              <div class="health-item">
                <span
                  class="health-dot"
                  [class.pass]="hc.status === 'Pass'"
                  [class.fail]="hc.status === 'Fail'"
                  [class.warn]="hc.status !== 'Pass' && hc.status !== 'Fail'"
                ></span>
                <div class="health-info">
                  <span class="health-name">{{
                    hc.description || hc.status
                  }}</span>
                  @if (hc.duration) {
                    <span class="health-duration">{{ hc.duration }}</span>
                  }
                </div>
              </div>
            }
          </div>
        </section>
      }

      <mat-divider></mat-divider>

      <!-- Environment Variables -->
      @if (envVars.length > 0) {
        <section class="detail-section">
          <h3 class="section-title">
            {{ 'dashboard.resources.envVars' | hhTranslate: 'Biến môi trường' }}
          </h3>
          <div class="env-list">
            @for (ev of envVars; track ev.key) {
              <div class="env-item">
                <span class="env-key">{{ ev.key }}</span>
                <span class="env-value">{{ ev.value }}</span>
              </div>
            }
          </div>
        </section>
      }

      <mat-divider></mat-divider>

      <!-- Replicas & Uptime -->
      <section class="detail-section">
        <h3 class="section-title">
          {{
            'dashboard.resources.operations' | hhTranslate: 'Thông tin vận hành'
          }}
        </h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">{{
              'dashboard.resources.replicas' | hhTranslate: 'Số replicas'
            }}</span>
            <span class="info-value">{{ instanceCount }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Uptime</span>
            <span class="info-value">—</span>
          </div>
        </div>
      </section>

      <mat-dialog-actions align="end">
        <button mat-stroked-button (click)="close()">
          {{ 'dashboard.resources.close' | hhTranslate: 'Đóng' }}
        </button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [
    `
      .detail-overlay {
        min-width: 480px;
        max-width: 600px;
        padding: 0;
      }
      .detail-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 16px;
        padding: 20px 24px;
      }
      .detail-header-left {
        display: flex;
        align-items: center;
        gap: 12px;
      }
      .detail-icon {
        font-size: 32px;
        width: 32px;
        height: 32px;
        color: var(--color-primary, #2f6b4a);
      }
      .detail-title {
        font-size: 18px;
        font-weight: 600;
        margin: 0;
        color: var(--text-primary, #1a1a1a);
      }
      .detail-subtitle {
        font-size: 13px;
        color: var(--text-secondary, #787774);
      }
      .detail-section {
        padding: 16px 24px;
      }
      .section-title {
        font-size: 11px;
        font-weight: 600;
        text-transform: uppercase;
        letter-spacing: 0.05em;
        color: var(--text-muted, #a1a09b);
        margin: 0 0 12px;
      }
      .info-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 12px;
      }
      .info-item {
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .info-label {
        font-size: 11px;
        color: var(--text-muted, #a1a09b);
        text-transform: uppercase;
        letter-spacing: 0.03em;
      }
      .info-value {
        font-size: 14px;
        font-weight: 500;
        color: var(--text-primary, #1a1a1a);
      }
      .text-green {
        color: var(--color-success);
      }
      .text-red {
        color: var(--color-danger);
      }
      .endpoint-list {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
      .endpoint-item {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 13px;
      }
      .endpoint-item mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
        color: var(--text-muted, #a1a09b);
      }
      .endpoint-item code {
        background: var(--bg-warm, #f7f6f3);
        padding: 2px 8px;
        border-radius: 4px;
        font-family: var(--font-mono);
        font-size: 12px;
      }
      .health-list {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
      .health-item {
        display: flex;
        align-items: center;
        gap: 10px;
      }
      .health-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--text-muted, #a1a09b);
        flex-shrink: 0;
      }
      .health-dot.pass {
        background: var(--color-success);
      }
      .health-dot.fail {
        background: var(--color-danger);
      }
      .health-dot.warn {
        background: var(--color-warning);
      }
      .health-info {
        display: flex;
        flex-direction: column;
        gap: 1px;
      }
      .health-name {
        font-size: 13px;
        font-weight: 500;
        color: var(--text-primary, #1a1a1a);
      }
      .health-duration {
        font-size: 11px;
        color: var(--text-muted, #a1a09b);
      }
      .env-list {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }
      .env-item {
        display: flex;
        gap: 12px;
        font-size: 12px;
        padding: 4px 0;
      }
      .env-key {
        font-family: var(--font-mono);
        color: var(--text-primary, #1a1a1a);
        font-weight: 500;
        min-width: 140px;
      }
      .env-value {
        font-family: var(--font-mono);
        color: var(--text-secondary, #787774);
        word-break: break-all;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceDetailComponent {
  readonly resource: Resource;

  constructor(
    @Inject(MAT_DIALOG_DATA) data: { resource: Resource },
    private readonly dialogRef: MatDialogRef<ResourceDetailComponent>,
  ) {
    this.resource = data.resource;
  }

  private readonly i18n = inject(HisHopeI18nService);

  statusLabel(status: string): string {
    return (
      (
        {
          Running: this.i18n.t(
            'dashboard.resources.status.running',
            'Đang chạy',
          ),
          Healthy: this.i18n.t(
            'dashboard.resources.status.healthy',
            'Khỏe mạnh',
          ),
          Stopped: this.i18n.t('dashboard.resources.status.stopped', 'Đã dừng'),
          Degraded: this.i18n.t(
            'dashboard.resources.status.degraded',
            'Suy giảm',
          ),
          Unhealthy: this.i18n.t(
            'dashboard.resources.status.unhealthy',
            'Mất sức khỏe',
          ),
          Unknown: this.i18n.t(
            'dashboard.resources.status.unknown',
            'Không xác định',
          ),
        } as Record<string, string>
      )[status] ?? status
    );
  }
  statusTone(status: string): HisHopeStatusTone {
    if (status === 'Running' || status === 'Healthy') return 'success';
    if (status === 'Degraded') return 'warning';
    if (status === 'Stopped') return 'neutral';
    return 'danger';
  }

  get endpoints(): string[] {
    const r = this.resource as unknown as Record<string, unknown>;
    if (Array.isArray(r['endpoints'])) return r['endpoints'] as string[];
    if (typeof r['baseUrl'] === 'string') return [r['baseUrl'] as string];
    return [];
  }

  get healthChecks(): HealthCheckResult[] {
    return this.resource.healthChecks ?? [];
  }

  get envVars(): Array<{ key: string; value: string }> {
    const tags = this.resource.tags;
    if (!tags) return [];
    return Object.entries(tags).map(([key, value]) => ({ key, value }));
  }

  get instanceCount(): number {
    const r = this.resource as unknown as Record<string, unknown>;
    if (typeof r['instanceCount'] === 'number')
      return r['instanceCount'] as number;
    return 1;
  }

  close(): void {
    this.dialogRef.close();
  }
}
