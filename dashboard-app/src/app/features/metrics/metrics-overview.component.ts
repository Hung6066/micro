import { Component, OnInit, ChangeDetectionStrategy, inject, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, of } from 'rxjs';
import { catchError, takeUntil } from 'rxjs/operators';
import { HisHopeMetricCardComponent, HisHopeTranslatePipe } from '@his-hope/frontend-foundation';
import { ResourceService } from '../../core/services/resource.service';
import { Resource } from '../../core/models/resource.model';

@Component({
  selector: 'app-metrics-overview',
  standalone: true,
  imports: [
    CommonModule,
    HisHopeMetricCardComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <div class="stats-grid">
      <hh-metric-card icon="check_circle" [label]="'dashboard.metricsOverview.running' | hhTranslate:'Đang chạy'"
                      [value]="runningCount" link="/resources"
                      [action]="'dashboard.metricsOverview.viewResources' | hhTranslate:'Xem tài nguyên'" />
      <hh-metric-card icon="stop_circle" [label]="'dashboard.metricsOverview.stopped' | hhTranslate:'Đã dừng'"
                      [value]="stoppedCount" link="/resources" tone="danger"
                      [action]="'dashboard.metricsOverview.viewResources' | hhTranslate:'Xem tài nguyên'" />
      <hh-metric-card icon="warning" [label]="'dashboard.metricsOverview.degraded' | hhTranslate:'Suy giảm'"
                      [value]="degradedCount" link="/resources" tone="warning"
                      [action]="'dashboard.metricsOverview.viewResources' | hhTranslate:'Xem tài nguyên'" />
      <hh-metric-card icon="dns" [label]="'dashboard.metricsOverview.totalServices' | hhTranslate:'Tổng dịch vụ'"
                      [value]="totalCount" link="/resources" tone="info"
                      [action]="'dashboard.metricsOverview.viewResources' | hhTranslate:'Xem tài nguyên'" />
    </div>
  `,
  styles: [`
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    @media (max-width: 900px) {
      .stats-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    }
    @media (max-width: 600px) {
      .stats-grid { grid-template-columns: 1fr; }
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetricsOverviewComponent implements OnInit, OnDestroy {
  private readonly resourceService = inject(ResourceService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroy$ = new Subject<void>();

  runningCount = 0;
  stoppedCount = 0;
  degradedCount = 0;
  totalCount = 0;

  ngOnInit(): void {
    this.loadResources();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadResources(): void {
    this.resourceService.getAll().pipe(
      catchError(() => of([] as Resource[])),
      takeUntil(this.destroy$),
    ).subscribe(resources => {
      const services = resources.filter(r => r.type?.toLowerCase() === 'service');
      this.totalCount = services.length;
      this.runningCount = services.filter(r => r.status === 'Running').length;
      this.stoppedCount = services.filter(r => r.status === 'Stopped').length;
      this.degradedCount = services.filter(
        r => r.status === 'Degraded' || r.healthStatus === 'Degraded' || r.healthStatus === 'Unhealthy'
      ).length;
      this.cdr.markForCheck();
    });
  }
}
