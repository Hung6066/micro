import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Resource } from '../../core/models/resource.model';
import { ServiceStatusBadgeComponent } from '../service-status-badge/service-status-badge.component';

@Component({
  selector: 'app-resource-card',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatTooltipModule,
    ServiceStatusBadgeComponent,
  ],
  template: `
    <mat-card class="resource-card" [class.pulsing]="animating" (click)="onCardClick()">
      <mat-card-content>
        <div class="card-header">
          <div class="card-header-left">
            <mat-icon class="resource-icon" [class]="'type-' + resource.type">
              {{ typeIcon }}
            </mat-icon>
            <div class="card-info">
              <span class="resource-name">{{ resource.displayName || resource.name }}</span>
              <span class="resource-type">{{ resource.type }}</span>
            </div>
          </div>
          <app-service-status-badge [state]="resource.status"></app-service-status-badge>
        </div>

        <mat-divider></mat-divider>

        <div class="card-meta">
          <div class="meta-item" *ngIf="resource.version">
            <span class="meta-label">Version</span>
            <span class="meta-value">{{ resource.version }}</span>
          </div>
          <div class="meta-item">
            <span class="meta-label">Health</span>
            <span class="meta-value health-value" [class.healthy]="resource.healthStatus === 'Healthy'"
                                                      [class.unhealthy]="resource.healthStatus === 'Unhealthy'"
                                                      [class.degraded]="resource.healthStatus === 'Degraded'">
              {{ resource.healthStatus }}
            </span>
          </div>
          <div class="meta-item" *ngIf="resource.type === 'Service' || resource.type === 'service'">
            <span class="meta-label">CPU</span>
            <span class="meta-value">{{ resource.cpuPercent != null ? (resource.cpuPercent | number:'1.1-1') + '%' : '—' }}</span>
          </div>
          <div class="meta-item" *ngIf="resource.type === 'Service' || resource.type === 'service'">
            <span class="meta-label">Memory</span>
            <span class="meta-value">{{ resource.memoryUsedMb != null ? (resource.memoryUsedMb | number:'1.0-0') + ' MB' : '—' }}</span>
          </div>
        </div>

        <mat-divider></mat-divider>

        <div class="card-actions" (click)="$event.stopPropagation()">
          <button mat-stroked-button size="small" (click)="onStart()"
                  [disabled]="resource.status === 'Running'">
            <mat-icon>play_arrow</mat-icon>
            Start
          </button>
          <button mat-stroked-button size="small" (click)="onStop()"
                  [disabled]="resource.status === 'Stopped'">
            <mat-icon>stop</mat-icon>
            Stop
          </button>
          <button mat-stroked-button size="small" (click)="onRestart()">
            <mat-icon>refresh</mat-icon>
            Restart
          </button>
        </div>

        <mat-divider *ngIf="resource.type === 'Service' || resource.type === 'service'"></mat-divider>

        <!-- Quick links to Logs, Traces, Metrics -->
        <div class="quick-links" *ngIf="resource.type === 'Service' || resource.type === 'service'"
             (click)="$event.stopPropagation()">
          <a mat-icon-button [routerLink]="['/logs']"
             [queryParams]="{ service: resource.name }"
             matTooltip="View logs"
             aria-label="View logs">
            <mat-icon>article</mat-icon>
          </a>
          <a mat-icon-button [routerLink]="['/traces']"
             [queryParams]="{ service: resource.name }"
             matTooltip="View traces"
             aria-label="View traces">
            <mat-icon>timeline</mat-icon>
          </a>
          <a mat-icon-button [routerLink]="['/metrics']"
             [queryParams]="{ service: resource.name }"
             matTooltip="View metrics"
             aria-label="View metrics">
            <mat-icon>monitoring</mat-icon>
          </a>
          <span class="quick-links-label">Logs, Traces, Metrics</span>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: [`
    @keyframes pulse-live {
      0% { border-color: var(--color-primary, #2F6B4A); box-shadow: 0 0 0 0 rgba(47, 107, 74, 0.3); }
      50% { border-color: var(--color-primary, #2F6B4A); box-shadow: 0 0 0 6px rgba(47, 107, 74, 0.08); }
      100% { border-color: var(--border-default, #EAEAEA); box-shadow: 0 0 0 0 rgba(47, 107, 74, 0); }
    }

    .resource-card {
      cursor: pointer;
      transition: transform 150ms ease, border-color 150ms ease;
      user-select: none;
    }
    .resource-card.pulsing {
      animation: pulse-live 600ms cubic-bezier(0.4, 0, 0.2, 1);
    }
    .resource-card:hover {
      border-color: var(--color-primary, #2F6B4A) !important;
    }
    .resource-card:active {
      transform: scale(0.98);
    }
    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 12px;
      margin-bottom: 12px;
    }
    .card-header-left {
      display: flex;
      align-items: center;
      gap: 12px;
      min-width: 0;
    }
    .resource-icon {
      font-size: 28px;
      width: 28px;
      height: 28px;
      color: var(--text-muted, #A1A09B);
      flex-shrink: 0;
    }
    .resource-icon.type-Service,
    .resource-icon.type-service {
      color: var(--color-primary, #2F6B4A);
    }
    .resource-icon.type-Database,
    .resource-icon.type-database {
      color: #2563EB;
    }
    .resource-icon.type-Infrastructure,
    .resource-icon.type-infrastructure {
      color: #6B4FA0;
    }
    .card-info {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .resource-name {
      font-size: 14px;
      font-weight: 600;
      color: var(--text-primary, #1A1A1A);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .resource-type {
      font-size: 12px;
      color: var(--text-secondary, #787774);
      margin-top: 1px;
    }
    .card-meta {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
      padding: 12px 0;
    }
    .meta-item {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .meta-label {
      font-size: 11px;
      color: var(--text-muted, #A1A09B);
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .meta-value {
      font-size: 13px;
      font-weight: 500;
      color: var(--text-primary, #1A1A1A);
    }
    .health-value.healthy { color: #2F6B4A; }
    .health-value.unhealthy { color: #C25450; }
    .health-value.degraded { color: #B6581C; }
    .card-actions {
      display: flex;
      gap: 8px;
      padding-top: 12px;
    }
    .card-actions button {
      flex: 1;
      min-width: 0;
      font-size: 12px;
      line-height: 1;
    }
    .card-actions button mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      margin-right: 2px;
    }
    .quick-links {
      display: flex;
      align-items: center;
      gap: 2px;
      padding-top: 8px;
    }
    .quick-links a {
      color: var(--text-muted, #A1A09B);
    }
    .quick-links a:hover {
      color: var(--color-primary, #2F6B4A);
    }
    .quick-links mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    .quick-links-label {
      font-size: 11px;
      color: var(--text-muted, #A1A09B);
      margin-left: 4px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceCardComponent implements OnChanges {
  @Input({ required: true }) resource!: Resource;
  @Input() pulseTrigger?: number;
  @Output() cardClick = new EventEmitter<Resource>();
  @Output() start = new EventEmitter<Resource>();
  @Output() stop = new EventEmitter<Resource>();
  @Output() restart = new EventEmitter<Resource>();

  animating = false;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['pulseTrigger'] && !changes['pulseTrigger'].firstChange) {
      this.animating = true;
      setTimeout(() => (this.animating = false), 700);
    }
  }

  get typeIcon(): string {
    const t = this.resource.type?.toLowerCase() ?? '';
    if (t === 'service') return 'dns';
    if (t === 'database') return 'storage';
    if (t === 'infrastructure') return 'cloud';
    return 'device_hub';
  }

  onCardClick(): void {
    this.cardClick.emit(this.resource);
  }

  onStart(): void {
    this.start.emit(this.resource);
  }

  onStop(): void {
    this.stop.emit(this.resource);
  }

  onRestart(): void {
    this.restart.emit(this.resource);
  }
}
