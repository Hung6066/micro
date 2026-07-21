import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatGridListModule } from '@angular/material/grid-list';
import { MatIconModule } from '@angular/material/icon';
import { Observable } from 'rxjs';
import { MetricsService } from '../../core/services/metrics.service';
import { MetricSnapshot } from '../../core/models/metric-snapshot.model';

@Component({
  selector: 'app-metrics-page',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatGridListModule,
    MatIconModule,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Chỉ số hệ thống</h1>
    </div>

    <div class="metrics-grid">
      <mat-card *ngFor="let metric of metrics$ | async" class="metric-card">
        <mat-card-content>
          <div class="metric-header">
            <span class="metric-name">{{ metric.displayName || metric.name }}</span>
            <span class="metric-unit">{{ metric.unit }}</span>
          </div>
          <div class="metric-value">{{ formatValue(metric.currentValue) }}</div>
          <div class="metric-change" *ngIf="metric.previousValue !== undefined">
            <span [class.up]="metric.currentValue > metric.previousValue"
                  [class.down]="metric.currentValue < metric.previousValue">
              {{ metric.currentValue - metric.previousValue > 0 ? '+' : ''
              }}{{ formatValue(metric.currentValue - metric.previousValue) }}
            </span>
            so với trước đó
          </div>
          <div class="metric-range" *ngIf="metric.min !== undefined">
            Min: {{ formatValue(metric.min) }} / Max: {{ formatValue(metric.max) }} / Avg: {{ formatValue(metric.avg) }}
          </div>
        </mat-card-content>
      </mat-card>
    </div>

    <div *ngIf="(metrics$ | async)?.length === 0" class="empty-state">
      <mat-icon>monitoring</mat-icon>
      <p>Chưa có chỉ số nào</p>
    </div>
  `,
  styles: [`
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
    .metric-card { cursor: default; }
    .metric-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    .metric-name { font-size: 13px; color: #787774; font-weight: 500; }
    .metric-unit { font-size: 11px; color: #A1A09B; text-transform: uppercase; }
    .metric-value { font-size: 32px; font-weight: 600; color: #1A1A1A; line-height: 1.2; margin-bottom: 4px; }
    .metric-change { font-size: 12px; color: #787774; }
    .metric-change .up { color: #C25450; }
    .metric-change .down { color: #2F6B4A; }
    .metric-range { font-size: 11px; color: #A1A09B; margin-top: 4px; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetricsPageComponent implements OnInit {
  readonly metrics$: Observable<MetricSnapshot[]>;

  constructor(private readonly metricsService: MetricsService) {
    this.metrics$ = this.metricsService.getSummary();
  }

  ngOnInit(): void {}

  formatValue(value: number | undefined | null): string {
    if (value == null) return '-';
    if (value >= 1_000_000_000) return (value / 1_000_000_000).toFixed(1) + 'B';
    if (value >= 1_000_000) return (value / 1_000_000).toFixed(1) + 'M';
    if (value >= 1_000) return (value / 1_000).toFixed(1) + 'K';
    return value.toLocaleString();
  }
}
