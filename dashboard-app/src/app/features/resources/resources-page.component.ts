import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ResourceService } from '../../core/services/resource.service';
import { LifecycleService } from '../../core/services/lifecycle.service';
import { MetricsStreamService } from '../../core/services/metrics-stream.service';
import { Resource } from '../../core/models/resource.model';
import { LiveMetricUpdate } from '../../core/models/live-metric-update.model';
import { ResourceCardComponent } from '../../shared/resource-card/resource-card.component';
import { ResourceDetailComponent } from './resource-detail.component';
import { HealthTimelineComponent } from './health-timeline.component';
import { DependencyGraphComponent } from './dependency-graph.component';
import { HisHopeResourceState } from '@his-hope/frontend-foundation/query';
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from '@his-hope/frontend-foundation/ui';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';

interface GroupedResources {
  services: Resource[];
  databases: Resource[];
  infrastructure: Resource[];
  total: number;
}

@Component({
  selector: 'app-resources-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatButtonToggleModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
    ResourceCardComponent,
    DependencyGraphComponent,
    HealthTimelineComponent,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          'dashboard.resources.pageTitle' | hhTranslate: 'System Resources'
        "
      >
        <mat-button-toggle-group
          [value]="viewMode()"
          (change)="viewMode.set($event.value)"
          class="view-toggle"
          aria-label="View mode"
        >
          <mat-button-toggle value="cards" aria-label="Card view">
            <mat-icon>grid_view</mat-icon>
            {{ 'dashboard.resources.cardView' | hhTranslate: 'Cards' }}
          </mat-button-toggle>
          <mat-button-toggle value="graph" aria-label="Graph view">
            <mat-icon>hub</mat-icon>
            {{ 'dashboard.resources.graphView' | hhTranslate: 'Graph' }}
          </mat-button-toggle>
        </mat-button-toggle-group>
        <button
          mat-stroked-button
          (click)="refresh()"
          [disabled]="resource.loading()"
        >
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.resources.refresh' | hhTranslate: 'Refresh' }}
        </button>
      </hh-page-header>

      <!-- Loading spinner -->
      @if (resource.loading() && !resource.error()) {
        <hh-state
          kind="loading"
          [message]="
            'dashboard.resources.loading' | hhTranslate: 'Loading resources...'
          "
        ></hh-state>
      }

      <!-- Error state -->
      @if (resource.error()) {
        <hh-state
          kind="error"
          icon="error_outline"
          [message]="resource.errorMessage()"
        >
          <button mat-raised-button color="primary" (click)="refresh()">
            <mat-icon>refresh</mat-icon>
            {{ 'dashboard.resources.retry' | hhTranslate: 'Retry' }}
          </button>
        </hh-state>
      }

      <!-- Card view -->
      @if (viewMode() === 'cards') {
        @if (grouped; as grouped) {
          <!-- Loading overlay for existing content -->
          @if (resource.loading() && !resource.error() && grouped.total > 0) {
            <div class="loading-overlay">
              <mat-spinner diameter="20"></mat-spinner>
              <span>{{
                'dashboard.resources.refreshing' | hhTranslate: 'Refreshing...'
              }}</span>
            </div>
          }

          <!-- Services -->
          @if (grouped.services.length > 0) {
            <section class="resource-group">
              <div class="group-header">
                <mat-icon>dns</mat-icon>
                <h2 class="group-title">
                  {{ 'dashboard.resources.services' | hhTranslate: 'Services' }}
                </h2>
                <span class="group-count">{{ grouped.services.length }}</span>
              </div>
              <div class="card-grid">
                @for (r of grouped.services; track r.name) {
                  <app-resource-card
                    [resource]="r"
                    [pulseTrigger]="pulseTriggerMap.get(r.name) ?? 0"
                    (cardClick)="openDetail($event)"
                    (start)="onStart($event)"
                    (stop)="onStop($event)"
                    (restart)="onRestart($event)"
                  >
                  </app-resource-card>
                }
              </div>
            </section>
          }

          <!-- Databases -->
          @if (grouped.databases.length > 0) {
            <section class="resource-group">
              <div class="group-header">
                <mat-icon>storage</mat-icon>
                <h2 class="group-title">
                  {{
                    'dashboard.resources.databases' | hhTranslate: 'Databases'
                  }}
                </h2>
                <span class="group-count">{{ grouped.databases.length }}</span>
              </div>
              <div class="card-grid">
                @for (r of grouped.databases; track r.name) {
                  <app-resource-card
                    [resource]="r"
                    (cardClick)="openDetail($event)"
                    (start)="onStart($event)"
                    (stop)="onStop($event)"
                    (restart)="onRestart($event)"
                  >
                  </app-resource-card>
                }
              </div>
            </section>
          }

          <!-- Infrastructure -->
          @if (grouped.infrastructure.length > 0) {
            <section class="resource-group">
              <div class="group-header">
                <mat-icon>cloud</mat-icon>
                <h2 class="group-title">
                  {{
                    'dashboard.resources.infrastructure'
                      | hhTranslate: 'Infrastructure'
                  }}
                </h2>
                <span class="group-count">{{
                  grouped.infrastructure.length
                }}</span>
              </div>
              <div class="card-grid">
                @for (r of grouped.infrastructure; track r.name) {
                  <app-resource-card
                    [resource]="r"
                    (cardClick)="openDetail($event)"
                    (start)="onStart($event)"
                    (stop)="onStop($event)"
                    (restart)="onRestart($event)"
                  >
                  </app-resource-card>
                }
              </div>
            </section>
          }

          <!-- Empty state when loaded with no data -->
          @if (grouped.total === 0) {
            <hh-state
              icon="inventory_2"
              [message]="
                'dashboard.resources.noResources'
                  | hhTranslate: 'No resources found'
              "
            ></hh-state>
          }
        }
      }

      <!-- Graph view -->
      @if (viewMode() === 'graph') {
        @if (resource.data(); as allResources) {
          <app-dependency-graph
            [resources]="allResources"
            (nodeClick)="openDetail($event)"
          >
          </app-dependency-graph>
        }
      }

      <!-- Health Timeline — always visible -->
      <app-health-timeline></app-health-timeline>
    </hh-page-layout>
  `,
  styles: [
    `
      .view-toggle {
        border: 1px solid var(--border-default, #eaeaea);
        border-radius: 6px;
        overflow: hidden;
      }
      .view-toggle .mat-button-toggle {
        font-size: 12px;
        font-weight: 500;
        color: var(--text-secondary, #787774);
      }
      .view-toggle .mat-button-toggle-checked {
        background: var(--color-primary, #2f6b4a);
        color: #ffffff;
      }
      .view-toggle .mat-button-toggle mat-icon {
        font-size: 16px;
        width: 16px;
        height: 16px;
        margin-right: 4px;
      }
      .loading-overlay {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 8px;
        padding: 8px;
        font-size: 13px;
        color: var(--text-secondary, #787774);
      }
      .resource-group {
        margin-bottom: 32px;
      }
      .group-header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 16px;
      }
      .group-header mat-icon {
        font-size: 20px;
        width: 20px;
        height: 20px;
        color: var(--text-secondary, #787774);
      }
      .group-title {
        font-size: 16px;
        font-weight: 600;
        color: var(--text-primary, #1a1a1a);
        margin: 0;
      }
      .group-count {
        font-size: 12px;
        color: var(--text-muted, #a1a09b);
        background: var(--bg-warm, #f7f6f3);
        padding: 0 8px;
        border-radius: 10px;
        line-height: 20px;
      }
      .card-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
        gap: 16px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourcesPageComponent implements OnInit, OnDestroy {
  private readonly resourceService = inject(ResourceService);
  private readonly lifecycleService = inject(LifecycleService);
  private readonly metricsStream = inject(MetricsStreamService);
  private readonly dialog = inject(MatDialog);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly viewMode = signal<'cards' | 'graph'>('cards');

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);
  private readonly destroy$ = new Subject<void>();

  /** Incrementing counter per service to pulse the card when live data arrives. */
  readonly pulseTriggerMap = new Map<string, number>();

  private resourceCache: Resource[] = [];

  private readonly destroyRef = inject(DestroyRef);

  readonly resource = new HisHopeResourceState<Resource[]>(this.destroyRef);

  constructor() {
    effect(() => {
      const resources = this.resource.data();
      if (resources) this.resourceCache = resources;
    });
  }

  get grouped(): GroupedResources | null {
    const resources = this.resource.data();
    if (!resources) return null;
    const grouped: GroupedResources = {
      services: [],
      databases: [],
      infrastructure: [],
      total: 0,
    };
    for (const r of resources) {
      const type = (r.type ?? '').toLowerCase();
      if (type === 'service' || type === 'services') {
        grouped.services.push(r);
      } else if (type === 'database' || type === 'databases') {
        grouped.databases.push(r);
      } else {
        grouped.infrastructure.push(r);
      }
    }
    grouped.total =
      grouped.services.length +
      grouped.databases.length +
      grouped.infrastructure.length;
    return grouped;
  }

  ngOnInit(): void {
    // Connect to SignalR metrics stream and subscribe to all service metrics
    this.metricsStream.connect().then(() => {
      const serviceNames = this.resourceCache
        .filter((r) => r.type?.toLowerCase() === 'service')
        .map((r) => r.name);
      if (serviceNames.length > 0) {
        this.metricsStream.subscribeMany(serviceNames);
      }
    });

    // Apply live metric updates to cached resources
    this.metricsStream.liveMetrics$
      .pipe(takeUntil(this.destroy$))
      .subscribe((update: LiveMetricUpdate) => {
        // Update cached resource's CPU and memory
        const resource = this.resourceCache.find(
          (r) => r.name === update.serviceName,
        );
        if (resource) {
          resource.cpuPercent = Math.round(update.cpu * 10) / 10;
          resource.memoryUsedMb = Math.round(update.memory);
        }

        // Increment pulse trigger for the card animation
        const current = this.pulseTriggerMap.get(update.serviceName) ?? 0;
        this.pulseTriggerMap.set(update.serviceName, current + 1);

        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  refresh(): void {
    this.resource.load(this.resourceService.getAll());
  }

  openDetail(resource: Resource): void {
    this.dialog.open(ResourceDetailComponent, {
      data: { resource },
      width: '600px',
      maxWidth: '90vw',
      panelClass: 'resource-detail-panel',
    });
  }

  onStart(resource: Resource): void {
    this.lifecycleService.start(resource.name).subscribe({
      next: () => this.refresh(),
      error: () => console.error(`Failed to start ${resource.name}`),
    });
  }

  onStop(resource: Resource): void {
    this.lifecycleService.stop(resource.name).subscribe({
      next: () => this.refresh(),
      error: () => console.error(`Failed to stop ${resource.name}`),
    });
  }

  onRestart(resource: Resource): void {
    this.lifecycleService.restart(resource.name).subscribe({
      next: () => this.refresh(),
      error: () => console.error(`Failed to restart ${resource.name}`),
    });
  }
}
