import { Component, OnInit, ChangeDetectionStrategy, inject, signal, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { BehaviorSubject, Observable, of, Subject } from 'rxjs';
import { catchError, finalize, map, shareReplay, switchMap, tap, takeUntil } from 'rxjs/operators';
import { ResourceService } from '../../core/services/resource.service';
import { LifecycleService } from '../../core/services/lifecycle.service';
import { MetricsStreamService } from '../../core/services/metrics-stream.service';
import { Resource } from '../../core/models/resource.model';
import { LiveMetricUpdate } from '../../core/models/live-metric-update.model';
import { ResourceCardComponent } from '../../shared/resource-card/resource-card.component';
import { ResourceDetailComponent } from './resource-detail.component';
import { HealthTimelineComponent } from './health-timeline.component';
import { DependencyGraphComponent } from './dependency-graph.component';
import { HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeStateComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';

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
    <hh-page-header hhPageHeader [title]="'dashboard.resources.pageTitle' | hhTranslate:'System Resources'">
        <mat-button-toggle-group
          [value]="viewMode()"
          (change)="viewMode.set($event.value)"
          class="view-toggle"
          aria-label="View mode">
          <mat-button-toggle value="cards" aria-label="Card view">
            <mat-icon>grid_view</mat-icon>
            {{ 'dashboard.resources.cardView' | hhTranslate:'Cards' }}
          </mat-button-toggle>
          <mat-button-toggle value="graph" aria-label="Graph view">
            <mat-icon>hub</mat-icon>
            {{ 'dashboard.resources.graphView' | hhTranslate:'Graph' }}
          </mat-button-toggle>
        </mat-button-toggle-group>
        <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.resources.refresh' | hhTranslate:'Refresh' }}
        </button>
    </hh-page-header>

    <!-- Loading spinner -->
    @if ((loading$ | async) && !(error$ | async)) {
      <hh-state kind="loading" [message]="'dashboard.resources.loading' | hhTranslate:'Loading resources...'"></hh-state>
    }

    <!-- Error state -->
    @if (error$ | async; as err) {
      <hh-state kind="error" icon="error_outline" [message]="err">
        <button mat-raised-button color="primary" (click)="refresh()">
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.resources.retry' | hhTranslate:'Retry' }}
        </button>
      </hh-state>
    }

    <!-- Card view -->
    @if (viewMode() === 'cards') {
      @if (grouped$ | async; as grouped) {
        <!-- Loading overlay for existing content -->
        @if ((loading$ | async) && !(error$ | async) && grouped.total > 0) {
          <div class="loading-overlay">
            <mat-spinner diameter="20"></mat-spinner>
            <span>{{ 'dashboard.resources.refreshing' | hhTranslate:'Refreshing...' }}</span>
          </div>
        }

        <!-- Services -->
        @if (grouped.services.length > 0) {
          <section class="resource-group">
            <div class="group-header">
              <mat-icon>dns</mat-icon>
              <h2 class="group-title">{{ 'dashboard.resources.services' | hhTranslate:'Services' }}</h2>
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
                  (restart)="onRestart($event)">
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
              <h2 class="group-title">{{ 'dashboard.resources.databases' | hhTranslate:'Databases' }}</h2>
              <span class="group-count">{{ grouped.databases.length }}</span>
            </div>
            <div class="card-grid">
              @for (r of grouped.databases; track r.name) {
                <app-resource-card
                  [resource]="r"
                  (cardClick)="openDetail($event)"
                  (start)="onStart($event)"
                  (stop)="onStop($event)"
                  (restart)="onRestart($event)">
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
              <h2 class="group-title">{{ 'dashboard.resources.infrastructure' | hhTranslate:'Infrastructure' }}</h2>
              <span class="group-count">{{ grouped.infrastructure.length }}</span>
            </div>
            <div class="card-grid">
              @for (r of grouped.infrastructure; track r.name) {
                <app-resource-card
                  [resource]="r"
                  (cardClick)="openDetail($event)"
                  (start)="onStart($event)"
                  (stop)="onStop($event)"
                  (restart)="onRestart($event)">
                </app-resource-card>
              }
            </div>
          </section>
        }

        <!-- Empty state when loaded with no data -->
        @if (grouped.total === 0) {
          <hh-state icon="inventory_2" [message]="'dashboard.resources.noResources' | hhTranslate:'No resources found'"></hh-state>
        }
      }
    }

    <!-- Graph view -->
    @if (viewMode() === 'graph') {
      @if (resources$ | async; as allResources) {
        <app-dependency-graph
          [resources]="allResources"
          (nodeClick)="openDetail($event)">
        </app-dependency-graph>
      }
    }

    <!-- Health Timeline — always visible -->
    <app-health-timeline></app-health-timeline>
    </hh-page-layout>
  `,
  styles: [`
    .view-toggle {
      border: 1px solid var(--border-default, #EAEAEA);
      border-radius: 6px;
      overflow: hidden;
    }
    .view-toggle .mat-button-toggle {
      font-size: 12px;
      font-weight: 500;
      color: var(--text-secondary, #787774);
    }
    .view-toggle .mat-button-toggle-checked {
      background: var(--color-primary, #2F6B4A);
      color: #FFFFFF;
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
      color: var(--text-primary, #1A1A1A);
      margin: 0;
    }
    .group-count {
      font-size: 12px;
      color: var(--text-muted, #A1A09B);
      background: var(--bg-warm, #F7F6F3);
      padding: 0 8px;
      border-radius: 10px;
      line-height: 20px;
    }
    .card-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 16px;
    }
  `],
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

  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly error$ = new BehaviorSubject<string | null>(null);

  readonly resources$: Observable<Resource[]> = this.refreshTrigger.pipe(
    tap(() => {
      this.loading$.next(true);
      this.error$.next(null);
    }),
    switchMap(() =>
      this.resourceService.getAll().pipe(
        catchError(err => {
          const msg = err?.message ?? err?.statusText ?? 'Failed to load resources. Please try again.';
          this.error$.next(msg);
          this.loading$.next(false);
          return of([] as Resource[]);
        }),
        tap(resources => {
          this.resourceCache = resources;
        }),
        finalize(() => {
          this.loading$.next(false);
        }),
      ),
    ),
    shareReplay(1),
  );

  readonly grouped$: Observable<GroupedResources> = this.resources$.pipe(
    map(resources => {
      const grouped: GroupedResources = { services: [], databases: [], infrastructure: [], total: 0 };
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
      grouped.total = grouped.services.length + grouped.databases.length + grouped.infrastructure.length;
      return grouped;
    }),
  );

  ngOnInit(): void {
    // Connect to SignalR metrics stream and subscribe to all service metrics
    this.metricsStream.connect().then(() => {
      const serviceNames = this.resourceCache
        .filter(r => r.type?.toLowerCase() === 'service')
        .map(r => r.name);
      if (serviceNames.length > 0) {
        this.metricsStream.subscribeMany(serviceNames);
      }
    });

    // Apply live metric updates to cached resources
    this.metricsStream.liveMetrics$
      .pipe(takeUntil(this.destroy$))
      .subscribe((update: LiveMetricUpdate) => {
        // Update cached resource's CPU and memory
        const resource = this.resourceCache.find(r => r.name === update.serviceName);
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
    this.refreshTrigger.next();
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
