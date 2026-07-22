import { Component, OnInit, ChangeDetectionStrategy, inject, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { catchError, finalize, takeUntil } from 'rxjs/operators';
import { SloService } from '../../core/services/slo.service';
import { SloRecord, SloResponse } from '../../core/models/slo.model';
import { MetricDataPoint } from '../../core/models/metric-snapshot.model';

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Map availability to a color key. */
function availabilityColor(avail: number): string {
  if (avail >= 99.9) return 'var(--color-green)';
  if (avail >= 99.0) return 'var(--color-orange)';
  return 'var(--color-red)';
}

/** Map availability to a background tint. */
function availabilityBg(avail: number): string {
  if (avail >= 99.9) return 'var(--pastel-green)';
  if (avail >= 99.0) return 'var(--pastel-orange)';
  return 'var(--pastel-red)';
}

/** Build SVG arc path for a top-semi-circle gauge (0–100, left→right via top). */
function gaugeArcPath(value: number): string {
  const cx = 50, cy = 50, r = 40;
  const normalized = Math.min(100, Math.max(0, value));
  const sweep = (normalized / 100) * Math.PI; // rad to sweep
  const endAngle = Math.PI - sweep;           // 0%:π (left), 100%:0 (right)

  const x1 = cx + r * Math.cos(Math.PI); // 10 (left)
  const y1 = cy + r * Math.sin(Math.PI); // 50
  const x2 = cx + r * Math.cos(endAngle);
  const y2 = cy + r * Math.sin(endAngle);

  const largeArc = sweep > Math.PI / 2 ? 1 : 0;
  // sweep-flag=0 = counter-clockwise (draws through the top in SVG y-down coords)
  return `M ${x1} ${y1} A ${r} ${r} 0 ${largeArc} 0 ${x2} ${y2}`;
}

/** Build SVG polyline points string from sparkline data. */
function sparklinePoints(data: MetricDataPoint[], width: number, height: number): string {
  if (!data || data.length < 2) return '';
  const values = data.map(d => d.value);
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
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Service Level Objectives</h1>
      <button mat-stroked-button (click)="refresh()" [disabled]="(loading$ | async) ?? false">
        <mat-icon>refresh</mat-icon>
        Refresh
      </button>
    </div>

    <!-- Loading -->
    <div class="loading-state" *ngIf="loading$ | async">
      <mat-spinner diameter="32"></mat-spinner>
      <span class="loading-text">Loading SLO data...</span>
    </div>

    <!-- Error -->
    <div class="error-state" *ngIf="error$ | async as err">
      <mat-icon class="error-icon">error_outline</mat-icon>
      <p class="error-message">{{ err }}</p>
      <button mat-raised-button color="primary" (click)="refresh()">Retry</button>
    </div>

    <!-- SLO cards grid -->
    <div class="slo-grid" *ngIf="!(loading$ | async) && !(error$ | async) && services.length > 0">
      <mat-card class="slo-card" *ngFor="let svc of services">
        <mat-card-header>
          <mat-card-title class="slo-title">{{ svc.displayName }}</mat-card-title>
          <mat-card-subtitle class="slo-subtitle">{{ svc.service }}</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content class="slo-content">
          <!-- Availability gauge (top semi-circle, 0–100%) -->
          <div class="gauge-wrapper">
            <svg viewBox="0 0 100 65" class="gauge-svg" aria-label="Availability gauge">
              <!-- Background arc (full grey semi-circle, CCW = top) -->
              <path d="M 10 50 A 40 40 0 1 0 90 50"
                    fill="none" stroke="#EAEAEA" stroke-width="6" stroke-linecap="round"/>
              <!-- Foreground arc (colored, partial) -->
              <path [attr.d]="gaugePath(svc.availability)"
                    fill="none"
                    [attr.stroke]="availColor(svc.availability)"
                    stroke-width="6"
                    stroke-linecap="round"
                    style="transition: d 600ms cubic-bezier(0.4, 0, 0.2, 1);"/>
            </svg>
            <div class="gauge-label">
              <span class="gauge-value" [style.color]="availColor(svc.availability)">
                {{ fmt(svc.availability, 2) }}%
              </span>
              <span class="gauge-meta">Availability</span>
            </div>
          </div>

          <!-- Error budget bar -->
          <div class="budget-row">
            <div class="budget-header">
              <span class="budget-label">Error Budget</span>
              <span class="budget-value" [style.color]="budgetColor(svc.errorBudgetRemaining)">
                {{ fmt(svc.errorBudgetRemaining, 1) }}%
              </span>
            </div>
            <div class="budget-track">
              <div class="budget-fill"
                   [style.width.%]="svc.errorBudgetRemaining"
                   [style.background]="budgetColor(svc.errorBudgetRemaining)"></div>
            </div>
          </div>

          <!-- Burn rate indicators -->
          <div class="burn-row">
            <div class="burn-item">
              <span class="burn-value" [style.color]="burnRateValue(svc.burnRate1h)">
                {{ fmt(svc.burnRate1h, 2) }}
              </span>
              <span class="burn-label">1h Burn</span>
            </div>
            <div class="burn-divider"></div>
            <div class="burn-item">
              <span class="burn-value" [style.color]="burnRateValue(svc.burnRate6h)">
                {{ fmt(svc.burnRate6h, 2) }}
              </span>
              <span class="burn-label">6h Burn</span>
            </div>
          </div>

          <!-- Latency P99 -->
          <div class="latency-row">
            <mat-icon class="latency-icon" [style.color]="latencyColor(svc.latencyP99)">timer</mat-icon>
            <span class="latency-value" [style.color]="latencyColor(svc.latencyP99)">
              {{ fmt(svc.latencyP99, 0) }}ms
            </span>
            <span class="latency-label">p99 latency</span>
          </div>
        </mat-card-content>
      </mat-card>
    </div>

    <!-- Latency sparkline card -->
    <mat-card class="sparkline-card" *ngIf="sparklineData && sparklineData.length > 1 && !(loading$ | async) && !(error$ | async)">
      <mat-card-header>
        <mat-card-title class="slo-title">p99 Latency Trend</mat-card-title>
        <mat-card-subtitle class="slo-subtitle">Last 24 hours</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <div class="sparkline-container">
          <svg viewBox="0 0 480 80" class="sparkline-svg" preserveAspectRatio="none">
            <polyline
              [attr.points]="sparklinePts"
              fill="none"
              stroke="#2F6B4A"
              stroke-width="2"
              vector-effect="non-scaling-stroke"/>
          </svg>
        </div>
      </mat-card-content>
    </mat-card>

    <!-- Empty state -->
    <div class="empty-state" *ngIf="!(loading$ | async) && !(error$ | async) && services.length === 0">
      <mat-icon>speed</mat-icon>
      <p>No SLO data available. Ensure Prometheus recording rules are configured.</p>
    </div>
  `,
  styles: [`
    :host {
      --color-green: #2F6B4A;
      --color-orange: #B6581C;
      --color-red: #C25450;
      --pastel-green: #EDF3EC;
      --pastel-orange: #FDF0E2;
      --pastel-red: #FDEBEC;
    }

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
    }

    .loading-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 64px 24px;
      color: #787774;
    }
    .loading-text { margin-top: 12px; font-size: 14px; }

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

    /* ── Grid ── */
    .slo-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }

    /* ── Card ── */
    .slo-card mat-card-content { padding: 0 20px 20px !important; }
    .slo-title { font-size: 15px; font-weight: 600; color: #1A1A1A; }
    .slo-subtitle { font-size: 11px; color: #787774; }

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
      font-weight: 700;
      line-height: 1.2;
    }
    .gauge-meta {
      font-size: 11px;
      color: #787774;
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
    .budget-label { font-size: 12px; color: #787774; }
    .budget-value { font-size: 13px; font-weight: 600; }
    .budget-track {
      height: 8px;
      background: #EAEAEA;
      border-radius: 4px;
      overflow: hidden;
    }
    .budget-fill {
      height: 100%;
      border-radius: 4px;
      transition: width 600ms cubic-bezier(0.4, 0, 0.2, 1);
    }

    /* ── Burn rate ── */
    .burn-row {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 16px;
      padding: 12px;
      background: #F7F6F3;
      border-radius: 6px;
    }
    .burn-item {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 2px;
    }
    .burn-value {
      font-size: 18px;
      font-weight: 700;
    }
    .burn-label {
      font-size: 10px;
      color: #787774;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .burn-divider {
      width: 1px;
      height: 32px;
      background: #D4D4D0;
    }

    /* ── Latency row ── */
    .latency-row {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 12px;
      background: #F7F6F3;
      border-radius: 6px;
    }
    .latency-icon { font-size: 18px; width: 18px; height: 18px; }
    .latency-value { font-size: 16px; font-weight: 600; }
    .latency-label { font-size: 11px; color: #787774; margin-left: auto; }

    /* ── Sparkline card ── */
    .sparkline-card { margin-bottom: 24px; }
    .sparkline-container {
      width: 100%;
      height: 80px;
    }
    .sparkline-svg {
      width: 100%;
      height: 100%;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SloPageComponent implements OnInit, OnDestroy {
  private readonly sloService = inject(SloService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();

  readonly loading$ = new BehaviorSubject<boolean>(true);
  readonly error$ = new BehaviorSubject<string | null>(null);

  services: SloRecord[] = [];
  sparklineData: MetricDataPoint[] | null = null;

  // Exposed template helpers
  readonly gaugePath = gaugeArcPath;
  readonly availColor = availabilityColor;
  readonly availBg = availabilityBg;
  readonly fmt = fmt;

  ngOnInit(): void {
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  refresh(): void {
    this.error$.next(null);
    this.load();
  }

  private load(): void {
    this.loading$.next(true);
    this.error$.next(null);

    this.sloService.getAll().pipe(
      catchError(err => {
        this.error$.next(err?.message ?? 'Failed to load SLO data');
        return of(null);
      }),
      finalize(() => this.loading$.next(false)),
      takeUntil(this.destroy$),
    ).subscribe(resp => {
      if (!resp) return;
      this.services = resp.services ?? [];
      this.sparklineData = resp.sparklineData ?? null;
      this.cdr.markForCheck();
    });
  }

  /** Color for error budget. */
  budgetColor(pct: number): string {
    return pct >= 50 ? 'var(--color-green)' : 'var(--color-red)';
  }

  /** Color for burn rate — lower is better. */
  burnRateValue(value: number): string {
    return value >= 1.0 ? 'var(--color-red)' : value >= 0.5 ? 'var(--color-orange)' : 'var(--color-green)';
  }

  /** Color for latency — lower is better. */
  latencyColor(ms: number): string {
    return ms >= 500 ? 'var(--color-red)' : ms >= 200 ? 'var(--color-orange)' : 'var(--color-green)';
  }

  /** Compute polyline points for the sparkline SVG. */
  get sparklinePts(): string {
    if (!this.sparklineData || this.sparklineData.length < 2) return '';
    const values = this.sparklineData.map(d => d.value);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    const w = 480;
    const h = 80;

    return values
      .map((v, i) => {
        const x = (i / (values.length - 1)) * w;
        const y = h - ((v - min) / range) * h;
        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  }
}
