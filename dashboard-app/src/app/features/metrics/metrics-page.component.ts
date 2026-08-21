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
  ViewChild,
  ElementRef,
  AfterViewInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { BehaviorSubject, Subject, of, combineLatest } from 'rxjs';
import { catchError, debounceTime, takeUntil } from 'rxjs/operators';
import { MetricsService } from '../../core/services/metrics.service';
import { MetricsStreamService } from '../../core/services/metrics-stream.service';
import { ResourceService } from '../../core/services/resource.service';
import { MetricSnapshot } from '../../core/models/metric-snapshot.model';
import { LiveMetricUpdate } from '../../core/models/live-metric-update.model';
import { Resource } from '../../core/models/resource.model';
import { MetricsOverviewComponent } from './metrics-overview.component';
import { themeColor } from '../../shared/theme-color';
import { HisHopeResourceState } from '@his-hope/frontend-foundation/query';
import {
  HisHopeFilterToolbarComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePageSectionComponent,
  HisHopeStateComponent,
} from '@his-hope/frontend-foundation/ui';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';
import {
  Chart,
  LineController,
  LineElement,
  PointElement,
  LinearScale,
  TimeScale,
  CategoryScale,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';

Chart.register(
  LineController,
  LineElement,
  PointElement,
  LinearScale,
  TimeScale,
  CategoryScale,
  Tooltip,
  Legend,
  Filler,
);

type MetricType = 'cpu' | 'memory' | 'requests' | 'errors';

interface MetricConfig {
  key: MetricType;
  label: string;
  unit: string;
  token: string;
}

/** Design tokens used for chart line colors. */
const METRIC_TYPES: MetricConfig[] = [
  { key: 'cpu', label: 'CPU', unit: '%', token: '--color-primary' },
  { key: 'memory', label: 'Memory', unit: 'MB', token: '--color-info' },
  {
    key: 'requests',
    label: 'Requests',
    unit: 'req/s',
    token: '--color-warning',
  },
  {
    key: 'errors',
    label: 'Errors',
    unit: 'errors/min',
    token: '--color-danger',
  },
];

const TIME_RANGES = [
  { value: '5m', label: '5 minutes' },
  { value: '15m', label: '15 minutes' },
  { value: '1h', label: '1 hour' },
  { value: '6h', label: '6 hours' },
  { value: '24h', label: '24 hours' },
];

const SERVICE_TOKENS = [
  '--color-primary',
  '--color-success',
  '--color-info',
  '--color-warning',
  '--color-danger',
];

interface ChartDataset {
  label: string;
  data: { x: string; y: number }[];
  borderColor: string;
  backgroundColor: string;
  fill: boolean;
  tension: number;
  pointRadius: number;
}

@Component({
  selector: 'app-metrics-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MetricsOverviewComponent,
    HisHopeFilterToolbarComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopePageSectionComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'dashboard.metrics.pageTitle' | hhTranslate: 'System Metrics'"
        [subtitle]="
          'dashboard.metrics.pageSubtitle'
            | hhTranslate: 'Real-time resource metrics across all services'
        "
      >
        @if (liveConnected) {
          <span class="live-badge"
            >● {{ 'dashboard.metrics.live' | hhTranslate: 'LIVE' }}</span
          >
        }
        <button
          mat-stroked-button
          (click)="refresh()"
          [disabled]="resource.loading()"
        >
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.metrics.refresh' | hhTranslate: 'Refresh' }}
        </button>
      </hh-page-header>

      <!-- Overview cards -->
      <app-metrics-overview></app-metrics-overview>

      <!-- Controls -->
      <hh-filter-toolbar label="Metric filters">
        <!-- Service multi-select -->
        <mat-form-field
          appearance="outline"
          subscriptSizing="dynamic"
          class="services-field"
        >
          <mat-label>{{
            'dashboard.metrics.service' | hhTranslate: 'Service'
          }}</mat-label>
          <mat-select
            [(ngModel)]="selectedServices"
            multiple
            (selectionChange)="onServicesChange()"
          >
            @for (svc of availableServices; track svc.name) {
              <mat-option [value]="svc.name">
                {{ svc.displayName || svc.name }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        <!-- Metric type selector -->
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{
            'dashboard.metrics.metricType' | hhTranslate: 'Metric Type'
          }}</mat-label>
          <mat-select
            [(ngModel)]="selectedMetricType"
            (selectionChange)="onMetricTypeChange()"
          >
            @for (mt of metricTypes; track mt.key) {
              <mat-option [value]="mt.key">
                {{ mt.label }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        <!-- Time range selector -->
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>{{
            'dashboard.metrics.timeRange' | hhTranslate: 'Time Range'
          }}</mat-label>
          <mat-select
            [(ngModel)]="selectedTimeRange"
            (selectionChange)="onTimeRangeChange()"
          >
            @for (tr of timeRanges; track tr.value) {
              <mat-option [value]="tr.value">
                {{ tr.label }}
              </mat-option>
            }
          </mat-select>
        </mat-form-field>

        <button mat-raised-button color="primary" (click)="applyFilters()">
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.metrics.apply' | hhTranslate: 'Apply' }}
        </button>
      </hh-filter-toolbar>

      <!-- Selected services chips -->
      @if (selectedServices.length > 0) {
        <div class="service-chips">
          @for (svc of selectedServices; track svc) {
            <span
              class="chip"
              [style.color]="serviceColor(svc)"
              [style.background]="serviceChipBg(svc)"
            >
              {{ svc }}
              <mat-icon class="chip-remove" (click)="removeService(svc)"
                >close</mat-icon
              >
            </span>
          }
        </div>
      } @else {
        <div class="service-chips empty-chips">
          <span class="chip-hint">{{
            'dashboard.metrics.selectServiceHint'
              | hhTranslate: 'Select at least one service to view metrics'
          }}</span>
        </div>
      }

      <!-- Loading -->
      @let loading = resource.loading();
      @let error = resource.error() ? resource.errorMessage() : '';

      @if (loading) {
        <hh-state
          kind="loading"
          [message]="
            'dashboard.metrics.loading' | hhTranslate: 'Loading metrics...'
          "
        />
      }

      <!-- Error -->
      @if (error; as err) {
        <hh-state kind="error" icon="error_outline" [message]="err">
          <button mat-raised-button color="primary" (click)="refresh()">
            {{ 'dashboard.metrics.retry' | hhTranslate: 'Retry' }}
          </button>
        </hh-state>
      }

      <!-- Chart -->
      @if (hasData && !loading) {
        <hh-page-section
          [title]="currentMetric.label"
          [subtitle]="
            ('dashboard.metrics.timeRangeLabel' | hhTranslate: 'Time range') +
            ': ' +
            getTimeRangeLabel() +
            ' — ' +
            selectedServices.length +
            ' ' +
            ('dashboard.metrics.servicesSelected'
              | hhTranslate: 'services selected')
          "
        >
          <div class="chart-wrapper">
            <canvas #chartCanvas></canvas>
          </div>
        </hh-page-section>
      }

      <!-- Empty state -->
      @if (!hasData && !loading && !error) {
        <hh-state
          kind="empty"
          icon="monitoring"
          [message]="
            'dashboard.metrics.emptyState'
              | hhTranslate: 'Select services and metric to view chart'
          "
        />
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .live-badge {
        font-size: var(--font-size-nav);
        font-weight: var(--font-weight-semibold);
        color: var(--color-success);
        background: var(--surface-success);
        padding: var(--space-hairline) var(--space-sm);
        border-radius: var(--radius-badge);
        letter-spacing: 0.04em;
        animation: live-pulse 2s ease-in-out infinite;
      }
      @keyframes live-pulse {
        0%,
        100% {
          opacity: 1;
        }
        50% {
          opacity: 0.6;
        }
      }

      .services-field {
        min-width: 220px;
        flex: 1.5;
      }

      .service-chips {
        display: flex;
        flex-wrap: wrap;
        gap: var(--space-xs);
      }
      .chip {
        display: inline-flex;
        align-items: center;
        gap: var(--space-2xs);
        padding: var(--space-hairline) var(--space-sm) var(--space-hairline) var(--space-inset);
        border-radius: var(--radius-badge);
        font-size: var(--font-size-caption);
        font-weight: var(--font-weight-medium);
      }
      .chip-remove {
        font-size: var(--font-size-body);
        width: var(--size-timeline-dot);
        height: var(--size-timeline-dot);
        cursor: pointer;
        opacity: 0.6;
      }
      .chip-remove:hover {
        opacity: 1;
      }
      .empty-chips {
        margin-top: var(--space-2xs);
      }
      .chip-hint {
        font-size: var(--font-size-caption);
        color: var(--text-muted);
        font-style: italic;
      }

      .chart-wrapper {
        position: relative;
        width: 100%;
        min-height: 350px;
        margin-top: var(--space-sm);
      }
      .chart-wrapper canvas {
        width: 100% !important;
        height: 100% !important;
        max-height: 400px;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetricsPageComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild('chartCanvas') chartCanvas!: ElementRef<HTMLCanvasElement>;

  private readonly metricsService = inject(MetricsService);
  private readonly metricsStream = inject(MetricsStreamService);
  private readonly resourceService = inject(ResourceService);
  private readonly route = inject(ActivatedRoute);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();

  liveConnected = false;
  /** Latest live metric values keyed by service name. */
  readonly latestLiveMetrics = new Map<string, LiveMetricUpdate>();

  private readonly refreshTrigger = new BehaviorSubject<void>(undefined);

  private readonly destroyRef = inject(DestroyRef);

  readonly resource = new HisHopeResourceState<MetricSnapshot[][]>(
    this.destroyRef,
  );

  selectedServices: string[] = [];
  // Request metrics are emitted by every instrumented service in the
  // current K3s scrape contract. CPU/memory require a separate cAdvisor
  // scrape and are intentionally selectable but not the empty default.
  selectedMetricType: MetricType = 'requests';
  selectedTimeRange = '1h';

  availableServices: Resource[] = [];
  metricTypes = METRIC_TYPES;
  timeRanges = TIME_RANGES;
  hasData = false;

  currentMetric =
    METRIC_TYPES.find((m) => m.key === 'requests') ?? METRIC_TYPES[0];

  private chart: Chart | null = null;

  constructor() {
    effect(() => {
      const results = this.resource.data();
      if (results) this.renderChart(results);
    });
  }

  private readonly query$ = this.refreshTrigger.pipe(debounceTime(100));

  ngOnInit(): void {
    this.currentMetric =
      METRIC_TYPES.find((m) => m.key === this.selectedMetricType) ??
      METRIC_TYPES[0];

    // Connect to real-time metrics stream
    this.metricsStream.connect().then(() => {
      this.liveConnected = true;
      this.cdr.markForCheck();
      // Subscribe to all available services
      const svcNames = this.availableServices.map((s) => s.name);
      if (svcNames.length > 0) {
        this.metricsStream.subscribeMany(svcNames);
      }
    });

    // Collect live metric updates
    this.metricsStream.liveMetrics$
      .pipe(takeUntil(this.destroy$))
      .subscribe((update: LiveMetricUpdate) => {
        this.latestLiveMetrics.set(update.serviceName, update);
        this.cdr.markForCheck();
      });

    // Load services from ResourceService
    this.resourceService
      .getAll()
      .pipe(
        catchError(() => of([] as Resource[])),
        takeUntil(this.destroy$),
      )
      .subscribe((resources) => {
        this.availableServices = resources.filter(
          (r) => r.type?.toLowerCase() === 'service',
        );
        // Pre-select service from query param (Resource card quick-link)
        const svc = this.route.snapshot.queryParamMap.get('service');
        if (svc && !this.selectedServices.includes(svc)) {
          this.selectedServices = [svc];
          this.applyFilters();
        }
        this.cdr.markForCheck();
      });
  }

  ngAfterViewInit(): void {
    this.query$.subscribe(() => {
      this.loadChartData();
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.destroyChart();
  }

  /** Deterministically map a service name to a theme token. */
  private serviceToken(service: string): string {
    let hash = 0;
    for (let i = 0; i < service.length; i++) {
      hash = (hash << 5) - hash + service.charCodeAt(i);
      hash |= 0;
    }
    return SERVICE_TOKENS[Math.abs(hash) % SERVICE_TOKENS.length];
  }

  /** CSS var() string for a service (used in template chips). */
  serviceColor(service: string): string {
    return `var(${this.serviceToken(service)})`;
  }

  serviceChipBg(service: string): string {
    return `color-mix(in srgb, ${this.serviceColor(service)} 12%, transparent)`;
  }

  getTimeRangeLabel(): string {
    return (
      TIME_RANGES.find((r) => r.value === this.selectedTimeRange)?.label ??
      this.selectedTimeRange
    );
  }

  onServicesChange(): void {
    if (this.selectedServices.length > 0) {
      this.applyFilters();
    }
  }

  onMetricTypeChange(): void {
    this.currentMetric =
      METRIC_TYPES.find((m) => m.key === this.selectedMetricType) ??
      METRIC_TYPES[0];
    if (this.selectedServices.length > 0) {
      this.applyFilters();
    }
  }

  onTimeRangeChange(): void {
    if (this.selectedServices.length > 0) {
      this.applyFilters();
    }
  }

  removeService(service: string): void {
    this.selectedServices = this.selectedServices.filter((s) => s !== service);
    if (this.selectedServices.length > 0) {
      this.applyFilters();
    } else {
      this.hasData = false;
      this.destroyChart();
    }
  }

  applyFilters(): void {
    if (this.selectedServices.length === 0) return;
    this.refreshTrigger.next();
  }

  refresh(): void {
    if (this.selectedServices.length > 0) {
      this.refreshTrigger.next();
    }
  }

  private loadChartData(): void {
    if (this.selectedServices.length === 0) return;

    const metricKey = this.selectedMetricType;
    const requests = this.selectedServices.map((service) =>
      this.metricsService
        .getServiceMetrics(service, [metricKey], this.selectedTimeRange)
        .pipe(catchError(() => of([] as MetricSnapshot[]))),
    );

    // Use combineLatest to load all services in parallel
    this.resource.load(combineLatest(requests).pipe(takeUntil(this.destroy$)));
  }

  private renderChart(allMetrics: MetricSnapshot[][]): void {
    this.destroyChart();

    // Build datasets: one line per service
    const datasets: ChartDataset[] = [];
    const allLabels = new Set<string>();

    for (let i = 0; i < allMetrics.length; i++) {
      const snapshots = allMetrics[i];
      const service = this.selectedServices[i] ?? `Service ${i}`;
      const color = themeColor(this.serviceToken(service), '#5F6D65');

      // Collect all data points from snapshots
      const points: { x: string; y: number }[] = [];

      for (const snap of snapshots) {
        if (snap.dataPoints && snap.dataPoints.length > 0) {
          for (const dp of snap.dataPoints) {
            const label = new Date(dp.timestamp).toLocaleTimeString('en-US', {
              hour: '2-digit',
              minute: '2-digit',
              second: '2-digit',
            });
            allLabels.add(label);
            points.push({ x: label, y: dp.value });
          }
        } else {
          // Use currentValue if no dataPoints
          const label = 'now';
          allLabels.add(label);
          points.push({ x: label, y: snap.currentValue });
        }
      }

      if (points.length > 0) {
        datasets.push({
          label: service,
          data: points,
          borderColor: color,
          backgroundColor: color + '20',
          fill: false,
          tension: 0.3,
          pointRadius: 3,
        });
      }
    }

    if (datasets.length === 0) {
      this.hasData = false;
      this.cdr.markForCheck();
      return;
    }

    this.hasData = true;
    this.cdr.markForCheck();

    // Defer chart creation to next tick so *ngIf renders the canvas first
    setTimeout(() => this.createChart(datasets), 0);
  }

  private createChart(datasets: ChartDataset[]): void {
    if (!this.chartCanvas) return;

    const ctx = this.chartCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        datasets: datasets as any,
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: { duration: 300 },
        interaction: {
          intersect: false,
          mode: 'index',
        },
        plugins: {
          legend: {
            position: 'bottom',
            labels: {
              padding: 16,
              usePointStyle: true,
              font: { size: 12 },
            },
          },
          tooltip: {
            backgroundColor: themeColor('--shell-header-bg', '#123B29'),
            titleFont: { size: 12 },
            bodyFont: { size: 12 },
            padding: 10,
            cornerRadius: 6,
          },
        },
        scales: {
          x: {
            display: true,
            grid: { display: false },
            ticks: {
              maxTicksLimit: 10,
              font: { size: 11 },
              color: themeColor('--text-secondary', '#5F6D65'),
            },
          },
          y: {
            display: true,
            beginAtZero: true,
            grid: {
              color: themeColor('--border-light', '#EAF0EB'),
            },
            ticks: {
              font: { size: 11 },
              color: themeColor('--text-secondary', '#5F6D65'),
              callback: (value) => {
                const v = Number(value);
                if (v >= 1000) return (v / 1000).toFixed(1) + 'k';
                return v.toString();
              },
            },
          },
        },
      },
    });
    this.cdr.markForCheck();
  }

  private destroyChart(): void {
    if (this.chart) {
      this.chart.destroy();
      this.chart = null;
    }
  }
}
