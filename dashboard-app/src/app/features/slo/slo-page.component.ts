import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  effect,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subject } from 'rxjs';
import { HisHopeResourceState } from '@his-hope/frontend-foundation/query';
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from '@his-hope/frontend-foundation/ui';
import { HisHopeTranslatePipe } from '@his-hope/frontend-foundation/i18n';
import { SloService } from '../../core/services/slo.service';
import { SloRecord, SloResponse } from '../../core/models/slo.model';
import { MetricDataPoint } from '../../core/models/metric-snapshot.model';

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Map availability to a foundation color token. */
function availabilityColor(avail: number): string {
  if (avail >= 99.9) return 'var(--color-success)';
  if (avail >= 99.0) return 'var(--color-warning)';
  return 'var(--color-danger)';
}

/** Map availability to a foundation surface token. */
function availabilityBg(avail: number): string {
  if (avail >= 99.9) return 'var(--surface-success)';
  if (avail >= 99.0) return 'var(--surface-warning)';
  return 'var(--surface-danger)';
}

/** Build SVG arc path for a top-semi-circle gauge (0–100, left→right via top). */
function gaugeArcPath(value: number): string {
  const cx = 50,
    cy = 50,
    r = 40;
  const normalized = Math.min(100, Math.max(0, value));
  const sweep = (normalized / 100) * Math.PI; // rad to sweep
  const endAngle = Math.PI - sweep; // 0%:π (left), 100%:0 (right)

  const x1 = cx + r * Math.cos(Math.PI); // 10 (left)
  const y1 = cy + r * Math.sin(Math.PI); // 50
  const x2 = cx + r * Math.cos(endAngle);
  const y2 = cy + r * Math.sin(endAngle);

  const largeArc = sweep > Math.PI / 2 ? 1 : 0;
  // sweep-flag=0 = counter-clockwise (draws through the top in SVG y-down coords)
  return `M ${x1} ${y1} A ${r} ${r} 0 ${largeArc} 0 ${x2} ${y2}`;
}

/** Build SVG polyline points string from sparkline data. */
function sparklinePoints(
  data: MetricDataPoint[],
  width: number,
  height: number,
): string {
  if (!data || data.length < 2) return '';
  const values = data.map((d) => d.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;

  return values
    .map((v, i) => {
      const x = (i / (values.length - 1)) * width;
      const y = height - ((v - min) / range) * height;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}

/** Format a number for display. */
function fmt(val: number, decimals = 2): string {
  return val.toFixed(decimals);
}

// ─── Component ────────────────────────────────────────────────────────────────

@Component({
  selector: 'app-slo-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="
          'dashboard.slo.pageTitle' | hhTranslate: 'Service Level Objectives'
        "
        [subtitle]="
          'dashboard.slo.pageSubtitle'
            | hhTranslate
              : 'Availability, error budgets and latency across services'
        "
      >
        <button
          mat-stroked-button
          (click)="refresh()"
          [disabled]="resource.loading()"
        >
          <mat-icon>refresh</mat-icon>
          {{ 'dashboard.slo.refresh' | hhTranslate: 'Refresh' }}
        </button>
      </hh-page-header>

      @let loading = resource.loading();
      @let error = resource.error() ? resource.errorMessage() : '';

      <!-- Loading -->
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="
            'dashboard.slo.loading' | hhTranslate: 'Loading SLO data...'
          "
        />
      }

      <!-- Error -->
      @if (error; as err) {
        <hh-state kind="error" icon="error_outline" [message]="err">
          <button mat-raised-button color="primary" (click)="refresh()">
            {{ 'dashboard.slo.retry' | hhTranslate: 'Retry' }}
          </button>
        </hh-state>
      }

      <!-- SLO cards grid -->
      @if (!loading && !error && services.length > 0) {
        <div class="slo-grid">
          @for (svc of services; track svc.service) {
            <mat-card class="slo-card">
              <mat-card-header>
                <mat-card-title class="slo-title">{{
                  svc.displayName
                }}</mat-card-title>
                <mat-card-subtitle class="slo-subtitle">{{
                  svc.service
                }}</mat-card-subtitle>
              </mat-card-header>

              <mat-card-content class="slo-content">
                <!-- Availability gauge (top semi-circle, 0–100%) -->
                <div class="gauge-wrapper">
                  <svg
                    viewBox="0 0 100 65"
                    class="gauge-svg"
                    [attr.aria-label]="
                      'dashboard.slo.availability'
                        | hhTranslate: 'Availability gauge'
                    "
                  >
                    <!-- Background arc (full grey semi-circle, CCW = top) -->
                    <path
                      d="M 10 50 A 40 40 0 1 0 90 50"
                      fill="none"
                      [style.stroke]="'var(--border-default)'"
                      stroke-width="6"
                      stroke-linecap="round"
                    />
                    <!-- Foreground arc (colored, partial) -->
                    <path
                      [attr.d]="gaugePath(svc.availability)"
                      fill="none"
                      [style.stroke]="availColor(svc.availability)"
                      stroke-width="6"
                      stroke-linecap="round"
                      style="transition: d 600ms cubic-bezier(0.4, 0, 0.2, 1);"
                    />
                  </svg>
                  <div class="gauge-label">
                    <span
                      class="gauge-value"
                      [style.color]="availColor(svc.availability)"
                    >
                      {{ fmt(svc.availability, 2) }}%
                    </span>
                    <span class="gauge-meta">{{
                      'dashboard.slo.availability' | hhTranslate: 'Availability'
                    }}</span>
                  </div>
                </div>

                <!-- Error budget bar -->
                <div class="budget-row">
                  <div class="budget-header">
                    <span class="budget-label">{{
                      'dashboard.slo.errorBudget' | hhTranslate: 'Error Budget'
                    }}</span>
                    <span
                      class="budget-value"
                      [style.color]="budgetColor(svc.errorBudgetRemaining)"
                    >
                      {{ fmt(svc.errorBudgetRemaining, 1) }}%
                    </span>
                  </div>
                  <div class="budget-track">
                    <div
                      class="budget-fill"
                      [style.width.%]="svc.errorBudgetRemaining"
                      [style.background]="budgetColor(svc.errorBudgetRemaining)"
                    ></div>
                  </div>
                </div>

                <!-- Burn rate indicators -->
                <div class="burn-row">
                  <div class="burn-item">
                    <span
                      class="burn-value"
                      [style.color]="burnRateValue(svc.burnRate1h)"
                    >
                      {{ fmt(svc.burnRate1h, 2) }}
                    </span>
                    <span class="burn-label">{{
                      'dashboard.slo.burn1h' | hhTranslate: '1h Burn'
                    }}</span>
                  </div>
                  <div class="burn-divider"></div>
                  <div class="burn-item">
                    <span
                      class="burn-value"
                      [style.color]="burnRateValue(svc.burnRate6h)"
                    >
                      {{ fmt(svc.burnRate6h, 2) }}
                    </span>
                    <span class="burn-label">{{
                      'dashboard.slo.burn6h' | hhTranslate: '6h Burn'
                    }}</span>
                  </div>
                </div>

                <!-- Latency P99 -->
                <div class="latency-row">
                  <mat-icon
                    class="latency-icon"
                    [style.color]="latencyColor(svc.latencyP99)"
                    >timer</mat-icon
                  >
                  <span
                    class="latency-value"
                    [style.color]="latencyColor(svc.latencyP99)"
                  >
                    {{ fmt(svc.latencyP99, 0) }}ms
                  </span>
                  <span class="latency-label">{{
                    'dashboard.slo.p99Latency' | hhTranslate: 'p99 latency'
                  }}</span>
                </div>
              </mat-card-content>
            </mat-card>
          }
        </div>
      }

      <!-- Latency sparkline card -->
      @if (sparklineData && sparklineData.length > 1 && !loading && !error) {
        <mat-card class="sparkline-card">
          <mat-card-header>
            <mat-card-title class="slo-title">{{
              'dashboard.slo.latencyTrend' | hhTranslate: 'p99 Latency Trend'
            }}</mat-card-title>
            <mat-card-subtitle class="slo-subtitle">{{
              'dashboard.slo.last24h' | hhTranslate: 'Last 24 hours'
            }}</mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <div class="sparkline-container">
              <svg
                viewBox="0 0 480 80"
                class="sparkline-svg"
                preserveAspectRatio="none"
              >
                <polyline
                  [attr.points]="sparklinePts"
                  fill="none"
                  [style.stroke]="'var(--color-primary)'"
                  stroke-width="2"
                  vector-effect="non-scaling-stroke"
                />
              </svg>
            </div>
          </mat-card-content>
        </mat-card>
      }

      <!-- Empty state -->
      @if (!loading && !error && services.length === 0) {
        <hh-state
          kind="empty"
          icon="speed"
          [message]="
            'dashboard.slo.emptyState'
              | hhTranslate
                : 'No SLO data available. Ensure Prometheus recording rules are configured.'
          "
        />
      }
    </hh-page-layout>
  `,
  styles: [
    `
      /* ── Grid ── */
      .slo-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
        gap: 16px;
        margin-bottom: 24px;
      }

      /* ── Card ── */
      .slo-card mat-card-content {
        padding: 0 20px 20px !important;
      }
      .slo-title {
        font-size: 15px;
        font-weight: var(--font-weight-semibold);
        color: var(--text-primary);
      }
      .slo-subtitle {
        font-size: 11px;
        color: var(--text-secondary);
      }

      .slo-content {
        display: flex;
        flex-direction: column;
        gap: 16px;
      }

      /* ── Gauge ── */
      .gauge-wrapper {
        display: flex;
        flex-direction: column;
        align-items: center;
        position: relative;
      }
      .gauge-svg {
        width: 120px;
        height: 78px;
        overflow: visible;
      }
      .gauge-label {
        display: flex;
        flex-direction: column;
        align-items: center;
        margin-top: -4px;
      }
      .gauge-value {
        font-size: 22px;
        font-weight: var(--font-weight-bold);
        line-height: 1.2;
      }
      .gauge-meta {
        font-size: 11px;
        color: var(--text-secondary);
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      /* ── Error Budget ── */
      .budget-row {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }
      .budget-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
      }
      .budget-label {
        font-size: 12px;
        color: var(--text-secondary);
      }
      .budget-value {
        font-size: 13px;
        font-weight: var(--font-weight-semibold);
      }
      .budget-track {
        height: 8px;
        background: var(--border-default);
        border-radius: var(--radius-badge);
        overflow: hidden;
      }
      .budget-fill {
        height: 100%;
        border-radius: var(--radius-badge);
        transition: width 600ms cubic-bezier(0.4, 0, 0.2, 1);
      }

      /* ── Burn rate ── */
      .burn-row {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 16px;
        padding: 12px;
        background: var(--surface-muted);
        border-radius: var(--radius-input);
      }
      .burn-item {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 2px;
      }
      .burn-value {
        font-size: 18px;
        font-weight: var(--font-weight-bold);
      }
      .burn-label {
        font-size: 10px;
        color: var(--text-secondary);
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }
      .burn-divider {
        width: 1px;
        height: 32px;
        background: var(--border-strong);
      }

      /* ── Latency row ── */
      .latency-row {
        display: flex;
        align-items: center;
        gap: 6px;
        padding: 8px 12px;
        background: var(--surface-muted);
        border-radius: var(--radius-input);
      }
      .latency-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }
      .latency-value {
        font-size: 16px;
        font-weight: var(--font-weight-semibold);
      }
      .latency-label {
        font-size: 11px;
        color: var(--text-secondary);
        margin-left: auto;
      }

      /* ── Sparkline card ── */
      .sparkline-card {
        margin-bottom: 24px;
      }
      .sparkline-container {
        width: 100%;
        height: 80px;
      }
      .sparkline-svg {
        width: 100%;
        height: 100%;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SloPageComponent implements OnInit, OnDestroy {
  private readonly sloService = inject(SloService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();

  private readonly destroyRef = inject(DestroyRef);

  readonly resource = new HisHopeResourceState<SloResponse>(this.destroyRef);

  services: SloRecord[] = [];
  sparklineData: MetricDataPoint[] | null = null;

  // Exposed template helpers
  readonly gaugePath = gaugeArcPath;
  readonly availColor = availabilityColor;
  readonly availBg = availabilityBg;
  readonly fmt = fmt;

  constructor() {
    effect(() => {
      const resp = this.resource.data();
      if (!resp) return;
      this.services = resp.services ?? [];
      this.sparklineData = resp.sparklineData ?? null;
      this.cdr.markForCheck();
    });
  }

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  refresh(): void {
    this.load();
  }

  private load(): void {
    this.resource.load(this.sloService.getAll());
  }

  /** Color for error budget. */
  budgetColor(pct: number): string {
    return pct >= 50 ? 'var(--color-success)' : 'var(--color-danger)';
  }

  /** Color for burn rate — lower is better. */
  burnRateValue(value: number): string {
    return value >= 1.0
      ? 'var(--color-danger)'
      : value >= 0.5
        ? 'var(--color-warning)'
        : 'var(--color-success)';
  }

  /** Color for latency — lower is better. */
  latencyColor(ms: number): string {
    return ms >= 500
      ? 'var(--color-danger)'
      : ms >= 200
        ? 'var(--color-warning)'
        : 'var(--color-success)';
  }

  /** Compute polyline points for the sparkline SVG. */
  get sparklinePts(): string {
    if (!this.sparklineData || this.sparklineData.length < 2) return '';
    return sparklinePoints(this.sparklineData, 480, 80);
  }
}
