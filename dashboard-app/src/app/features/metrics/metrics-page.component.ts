import { Component, OnInit, ChangeDetectionStrategy, inject, OnDestroy, ViewChild, ElementRef, AfterViewInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { BehaviorSubject, Subject, of, combineLatest } from 'rxjs';
import { catchError, finalize, debounceTime, takeUntil } from 'rxjs/operators';
import { MetricsService } from '../../core/services/metrics.service';
import { MetricsStreamService } from '../../core/services/metrics-stream.service';
import { ResourceService } from '../../core/services/resource.service';
import { MetricSnapshot, MetricDataPoint } from '../../core/models/metric-snapshot.model';
import { LiveMetricUpdate } from '../../core/models/live-metric-update.model';
import { Resource } from '../../core/models/resource.model';
import { MetricsOverviewComponent } from './metrics-overview.component';
import { Chart, LineController, LineElement, PointElement, LinearScale, TimeScale, CategoryScale, Tooltip, Legend, Filler } from 'chart.js';

Chart.register(LineController, LineElement, PointElement, LinearScale, TimeScale, CategoryScale, Tooltip, Legend, Filler);

type MetricType = 'cpu' | 'memory' | 'requests' | 'errors';

interface MetricConfig {
  key: MetricType;
  label: string;
  unit: string;
  color: string;
}

const METRIC_TYPES: MetricConfig[] = [
  { key: 'cpu', label: 'CPU', unit: '%', color: '#2F6B4A' },
  { key: 'memory', label: 'Memory', unit: 'MB', color: '#2563EB' },
  { key: 'requests', label: 'Requests', unit: 'req/s', color: '#6B4FA0' },
  { key: 'errors', label: 'Errors', unit: 'errors/min', color: '#C25450' },
];

const TIME_RANGES = [
  { value: '5m', label: '5 minutes' },
  { value: '15m', label: '15 minutes' },
  { value: '1h', label: '1 hour' },
  { value: '6h', label: '6 hours' },
  { value: '24h', label: '24 hours' },
];

const SERVICE_COLORS = [
  '#2F6B4A', '#5B8C5A', '#2563EB', '#6B4FA0', '#B6581C',
  '#C25450', '#0D9488', '#7C3AED', '#0891B2', '#D97706',
];

@Component({
  selector: 'app-metrics-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatSelectModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MetricsOverviewComponent,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">
        System Metrics
        <span class="live-badge" *ngIf="liveConnected">● LIVE</span>
      </h1>
      <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
        <mat-icon>refresh</mat-icon>
        Refresh
      </button>
    </div>

    <!-- Overview cards -->
    <app-metrics-overview></app-metrics-overview>

    <!-- Controls card -->
    <mat-card class="controls-card">
      <mat-card-content>
        <div class="controls-row">
          <!-- Service multi-select -->
          <mat-form-field appearance="outline" subscriptSizing="dynamic" class="services-field">
            <mat-label>Service</mat-label>
            <mat-select [(ngModel)]="selectedServices" multiple (selectionChange)="onServicesChange()">
              <mat-option *ngFor="let svc of availableServices" [value]="svc.name">
                {{ svc.displayName || svc.name }}
              </mat-option>
            </mat-select>
          </mat-form-field>

          <!-- Metric type selector -->
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Metric Type</mat-label>
            <mat-select [(ngModel)]="selectedMetricType" (selectionChange)="onMetricTypeChange()">
              <mat-option *ngFor="let mt of metricTypes" [value]="mt.key">
                {{ mt.label }}
              </mat-option>
            </mat-select>
          </mat-form-field>

          <!-- Time range selector -->
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Time Range</mat-label>
            <mat-select [(ngModel)]="selectedTimeRange" (selectionChange)="onTimeRangeChange()">
              <mat-option *ngFor="let tr of timeRanges" [value]="tr.value">
                {{ tr.label }}
              </mat-option>
            </mat-select>
          </mat-form-field>

          <button mat-raised-button color="primary" (click)="applyFilters()">
            <mat-icon>refresh</mat-icon>
            Apply
          </button>
        </div>

        <!-- Selected services chips -->
        <div class="service-chips" *ngIf="selectedServices.length > 0">
          <span class="chip" *ngFor="let svc of selectedServices; let i = index"
                [style.--chip-color]="getServiceColor(svc)">
            {{ svc }}
            <mat-icon class="chip-remove" (click)="removeService(svc)">close</mat-icon>
          </span>
        </div>
        <div class="service-chips empty-chips" *ngIf="selectedServices.length === 0">
          <span class="chip-hint">Select at least one service to view metrics</span>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Loading -->
    <div class="loading-state" *ngIf="(loading$ | async)">
      <mat-spinner diameter="32"></mat-spinner>
      <span class="loading-text">Loading metrics...</span>
    </div>

    <!-- Error -->
    <div class="error-state" *ngIf="error$ | async as err">
      <mat-icon class="error-icon">error_outline</mat-icon>
      <p class="error-message">{{ err }}</p>
      <button mat-raised-button color="primary" (click)="refresh()">Retry</button>
    </div>

    <!-- Chart card -->
    <mat-card class="chart-card" *ngIf="hasData && !(loading$ | async)">
      <mat-card-header>
        <mat-card-title>{{ currentMetric.label }}</mat-card-title>
        <mat-card-subtitle>
          Time range: {{ getTimeRangeLabel() }} &mdash;
          {{ selectedServices.length }} services selected
        </mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <div class="chart-wrapper">
          <canvas #chartCanvas></canvas>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Empty state -->
    <div class="empty-state" *ngIf="!hasData && !(loading$ | async) && !(error$ | async)">
      <mat-icon>monitoring</mat-icon>
      <p>Select services and metric to view chart</p>
    </div>
  `,
  styles: [`
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .page-title {
      font-size: 20px;
      font-weight: 600;
      color: #1A1A1A;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .live-badge {
      font-size: 11px;
      font-weight: 600;
      color: #2F6B4A;
      background: #EDF3EC;
      padding: 2px 8px;
      border-radius: 4px;
      letter-spacing: 0.04em;
      animation: live-pulse 2s ease-in-out infinite;
    }
    @keyframes live-pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.6; }
    }
    .controls-card {
      margin-bottom: 16px;
    }
    .controls-row {
      display: flex;
      gap: 16px;
      align-items: flex-start;
      flex-wrap: wrap;
    }
    .controls-row mat-form-field {
      flex: 1;
      min-width: 160px;
    }
    .services-field {
      min-width: 220px;
      flex: 1.5;
    }
    .controls-row button {
      margin-top: 4px;
      height: 40px;
    }
    .service-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
      margin-top: 8px;
    }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 2px 8px 2px 10px;
      border-radius: 4px;
      font-size: 12px;
      font-weight: 500;
      color: var(--chip-color);
      background: color-mix(in srgb, var(--chip-color) 12%, transparent);
    }
    .chip-remove {
      font-size: 14px;
      width: 14px;
      height: 14px;
      cursor: pointer;
      opacity: 0.6;
    }
    .chip-remove:hover {
      opacity: 1;
    }
    .empty-chips {
      margin-top: 4px;
    }
    .chip-hint {
      font-size: 12px;
      color: #A1A09B;
      font-style: italic;
    }
    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 64px 24px;
      color: #787774;
    }
    .loading-text {
      margin-top: 12px;
      font-size: 14px;
    }
    .error-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 48px 24px;
      text-align: center;
      background: #FDEBEC;
      border: 1px solid #F5C6C4;
      border-radius: 8px;
      gap: 12px;
    }
    .error-icon { font-size: 40px; width: 40px; height: 40px; color: #C25450; }
    .error-message { font-size: 14px; color: #C25450; max-width: 400px; }
    .chart-card { margin-bottom: 16px; }
    .chart-wrapper {
      position: relative;
      width: 100%;
      min-height: 350px;
      margin-top: 8px;
    }
    .chart-wrapper canvas {
      width: 100% !important;
      height: 100% !important;
      max-height: 400px;
    }
    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 64px 24px;
      color: #A1A09B;
      text-align: center;
    }
    .empty-state mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      margin-bottom: 16px;
      opacity: 0.4;
    }
    .empty-state p { font-size: 14px; }
  `],
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

  readonly loading$ = new BehaviorSubject<boolean>(false);
  readonly error$ = new BehaviorSubject<string | null>(null);

  selectedServices: string[] = [];
  selectedMetricType: MetricType = 'cpu';
  selectedTimeRange = '1h';

  availableServices: Resource[] = [];
  metricTypes = METRIC_TYPES;
  timeRanges = TIME_RANGES;
  hasData = false;

  currentMetric = METRIC_TYPES[0];

  private chart: Chart | null = null;

  private readonly query$ = this.refreshTrigger.pipe(
    debounceTime(100),
  );

  ngOnInit(): void {
    this.currentMetric = METRIC_TYPES.find(m => m.key === this.selectedMetricType) ?? METRIC_TYPES[0];

    // Connect to real-time metrics stream
    this.metricsStream.connect().then(() => {
      this.liveConnected = true;
      this.cdr.markForCheck();
      // Subscribe to all available services
      const svcNames = this.availableServices.map(s => s.name);
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
    this.resourceService.getAll().pipe(
      catchError(() => of([] as Resource[])),
      takeUntil(this.destroy$),
    ).subscribe(resources => {
      this.availableServices = resources.filter(
        r => r.type?.toLowerCase() === 'service'
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

  getServiceColor(service: string): string {
    let hash = 0;
    for (let i = 0; i < service.length; i++) {
      hash = ((hash << 5) - hash) + service.charCodeAt(i);
      hash |= 0;
    }
    return SERVICE_COLORS[Math.abs(hash) % SERVICE_COLORS.length];
  }

  getTimeRangeLabel(): string {
    return TIME_RANGES.find(r => r.value === this.selectedTimeRange)?.label ?? this.selectedTimeRange;
  }

  onServicesChange(): void {
    if (this.selectedServices.length > 0) {
      this.applyFilters();
    }
  }

  onMetricTypeChange(): void {
    this.currentMetric = METRIC_TYPES.find(m => m.key === this.selectedMetricType) ?? METRIC_TYPES[0];
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
    this.selectedServices = this.selectedServices.filter(s => s !== service);
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
    this.error$.next(null);
    if (this.selectedServices.length > 0) {
      this.refreshTrigger.next();
    }
  }

  private loadChartData(): void {
    if (this.selectedServices.length === 0) return;

    this.loading$.next(true);
    this.error$.next(null);

    const metricKey = this.selectedMetricType;
    const requests = this.selectedServices.map(service =>
      this.metricsService.getServiceMetrics(service, [metricKey], this.selectedTimeRange).pipe(
        catchError(() => of([] as MetricSnapshot[])),
      )
    );

    // Use combineLatest to load all services in parallel
    const sub = combineLatest(requests).pipe(
      finalize(() => this.loading$.next(false)),
      takeUntil(this.destroy$),
    ).subscribe(results => {
      this.renderChart(results);
    });
  }

  private renderChart(allMetrics: MetricSnapshot[][]): void {
    this.destroyChart();

    // Build datasets: one line per service
    const datasets: { label: string; data: { x: string; y: number }[]; borderColor: string; backgroundColor: string; fill: boolean; tension: number; pointRadius: number; }[] = [];
    const allLabels = new Set<string>();

    for (let i = 0; i < allMetrics.length; i++) {
      const snapshots = allMetrics[i];
      const service = this.selectedServices[i] ?? `Service ${i}`;
      const color = this.getServiceColor(service);

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

  private createChart(datasets: { label: string; data: { x: string; y: number }[]; borderColor: string; backgroundColor: string; fill: boolean; tension: number; pointRadius: number; }[]): void {
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
            backgroundColor: '#1A1A1A',
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
              color: '#787774',
            },
          },
          y: {
            display: true,
            beginAtZero: true,
            grid: {
              color: '#EAEAEA',
            },
            ticks: {
              font: { size: 11 },
              color: '#787774',
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
