import { Component, Inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { Resource, HealthCheckResult } from '../../core/models/resource.model';
import { ServiceStatusBadgeComponent } from '../../shared/service-status-badge/service-status-badge.component';

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
    ServiceStatusBadgeComponent,
  ],
  template: `
    <div class="detail-overlay">
      <div class="detail-header">
        <div class="detail-header-left">
          <mat-icon class="detail-icon">dns</mat-icon>
          <div>
            <h2 class="detail-title">{{ resource.displayName || resource.name }}</h2>
            <span class="detail-subtitle">{{ resource.type }}</span>
          </div>
        </div>
        <app-service-status-badge [state]="resource.status"></app-service-status-badge>
      </div>

      <mat-divider></mat-divider>

      <!-- Basic Info -->
      <section class="detail-section">
        <h3 class="section-title">Thông tin cơ bản</h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">Tên</span>
            <span class="info-value">{{ resource.name }}</span>
          </div>
          <div class="info-item" *ngIf="resource.version">
            <span class="info-label">Phiên bản</span>
            <span class="info-value">{{ resource.version }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Trạng thái</span>
            <span class="info-value">{{ resource.status }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Sức khỏe</span>
            <span class="info-value" [class.text-green]="resource.healthStatus === 'Healthy'"
                                         [class.text-red]="resource.healthStatus === 'Unhealthy'">
              {{ resource.healthStatus }}
            </span>
          </div>
        </div>
      </section>

      <mat-divider></mat-divider>

      <!-- Endpoints -->
      <section class="detail-section" *ngIf="endpoints.length > 0">
        <h3 class="section-title">Endpoints</h3>
        <div class="endpoint-list">
          <div class="endpoint-item" *ngFor="let ep of endpoints">
            <mat-icon>link</mat-icon>
            <code>{{ ep }}</code>
          </div>
        </div>
      </section>

      <mat-divider></mat-divider>

      <!-- Health Checks -->
      <section class="detail-section" *ngIf="healthChecks.length > 0">
        <h3 class="section-title">Health Checks</h3>
        <div class="health-list">
          <div class="health-item" *ngFor="let hc of healthChecks">
            <span class="health-dot" [class.pass]="hc.status === 'Pass'"
                                     [class.fail]="hc.status === 'Fail'"
                                     [class.warn]="hc.status !== 'Pass' && hc.status !== 'Fail'"></span>
            <div class="health-info">
              <span class="health-name">{{ hc.description || hc.status }}</span>
              <span class="health-duration" *ngIf="hc.duration">{{ hc.duration }}</span>
            </div>
          </div>
        </div>
      </section>

      <mat-divider></mat-divider>

      <!-- Environment Variables -->
      <section class="detail-section" *ngIf="envVars.length > 0">
        <h3 class="section-title">Biến môi trường</h3>
        <div class="env-list">
          <div class="env-item" *ngFor="let ev of envVars">
            <span class="env-key">{{ ev.key }}</span>
            <span class="env-value">{{ ev.value }}</span>
          </div>
        </div>
      </section>

      <mat-divider></mat-divider>

      <!-- Replicas & Uptime -->
      <section class="detail-section">
        <h3 class="section-title">Thông tin vận hành</h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">Số replicas</span>
            <span class="info-value">{{ instanceCount }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Uptime</span>
            <span class="info-value">—</span>
          </div>
        </div>
      </section>

      <mat-dialog-actions align="end">
        <button mat-stroked-button (click)="close()">Đóng</button>
      </mat-dialog-actions>
    </div>
  `,
  styles: [`
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
      color: var(--color-primary, #2F6B4A);
    }
    .detail-title {
      font-size: 18px;
      font-weight: 600;
      margin: 0;
      color: var(--text-primary, #1A1A1A);
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
      color: var(--text-muted, #A1A09B);
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
      color: var(--text-muted, #A1A09B);
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .info-value {
      font-size: 14px;
      font-weight: 500;
      color: var(--text-primary, #1A1A1A);
    }
    .text-green { color: #2F6B4A; }
    .text-red { color: #C25450; }
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
      color: var(--text-muted, #A1A09B);
    }
    .endpoint-item code {
      background: var(--bg-warm, #F7F6F3);
      padding: 2px 8px;
      border-radius: 4px;
      font-family: var(--font-mono, monospace);
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
      background: var(--text-muted, #A1A09B);
      flex-shrink: 0;
    }
    .health-dot.pass { background: #2F6B4A; }
    .health-dot.fail { background: #C25450; }
    .health-dot.warn { background: #B6581C; }
    .health-info {
      display: flex;
      flex-direction: column;
      gap: 1px;
    }
    .health-name {
      font-size: 13px;
      font-weight: 500;
      color: var(--text-primary, #1A1A1A);
    }
    .health-duration {
      font-size: 11px;
      color: var(--text-muted, #A1A09B);
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
      font-family: var(--font-mono, monospace);
      color: var(--text-primary, #1A1A1A);
      font-weight: 500;
      min-width: 140px;
    }
    .env-value {
      font-family: var(--font-mono, monospace);
      color: var(--text-secondary, #787774);
      word-break: break-all;
    }
  `],
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
    if (typeof r['instanceCount'] === 'number') return r['instanceCount'] as number;
    return 1;
  }

  close(): void {
    this.dialogRef.close();
  }
}
