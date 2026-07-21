import { Component, OnInit, ChangeDetectionStrategy, inject, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BehaviorSubject, Subject, of } from 'rxjs';
import { catchError, finalize, takeUntil } from 'rxjs/operators';
import { ResourceService } from '../../core/services/resource.service';
import { Resource } from '../../core/models/resource.model';

@Component({
  selector: 'app-metrics-overview',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="overview-grid">
      <mat-card class="overview-card running">
        <mat-card-content>
          <div class="overview-inner">
            <div class="overview-icon">
              <mat-icon>check_circle</mat-icon>
            </div>
            <div class="overview-info">
              <span class="overview-value">{{ runningCount }}</span>
              <span class="overview-label">Đang chạy</span>
            </div>
          </div>
        </mat-card-content>
      </mat-card>

      <mat-card class="overview-card stopped">
        <mat-card-content>
          <div class="overview-inner">
            <div class="overview-icon">
              <mat-icon>stop_circle</mat-icon>
            </div>
            <div class="overview-info">
              <span class="overview-value">{{ stoppedCount }}</span>
              <span class="overview-label">Đã dừng</span>
            </div>
          </div>
        </mat-card-content>
      </mat-card>

      <mat-card class="overview-card degraded">
        <mat-card-content>
          <div class="overview-inner">
            <div class="overview-icon">
              <mat-icon>warning</mat-icon>
            </div>
            <div class="overview-info">
              <span class="overview-value">{{ degradedCount }}</span>
              <span class="overview-label">Suy giảm</span>
            </div>
          </div>
        </mat-card-content>
      </mat-card>

      <mat-card class="overview-card total">
        <mat-card-content>
          <div class="overview-inner">
            <div class="overview-icon">
              <mat-icon>dns</mat-icon>
            </div>
            <div class="overview-info">
              <span class="overview-value">{{ totalCount }}</span>
              <span class="overview-label">Tổng dịch vụ</span>
            </div>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .overview-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    .overview-card {
      cursor: default;
    }
    .overview-card mat-card-content {
      padding: 20px !important;
    }
    .overview-inner {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .overview-icon mat-icon {
      font-size: 36px;
      width: 36px;
      height: 36px;
    }
    .overview-card.running .overview-icon mat-icon { color: #2F6B4A; }
    .overview-card.stopped .overview-icon mat-icon { color: #787774; }
    .overview-card.degraded .overview-icon mat-icon { color: #B6581C; }
    .overview-card.total .overview-icon mat-icon { color: #2563EB; }
    .overview-info {
      display: flex;
      flex-direction: column;
    }
    .overview-value {
      font-size: 28px;
      font-weight: 700;
      color: #1A1A1A;
      line-height: 1.1;
    }
    .overview-label {
      font-size: 12px;
      color: #787774;
      margin-top: 2px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MetricsOverviewComponent implements OnInit, OnDestroy {
  private readonly resourceService = inject(ResourceService);
  private readonly destroy$ = new Subject<void>();

  runningCount = 0;
  stoppedCount = 0;
  degradedCount = 0;
  totalCount = 0;

  private readonly loading$ = new BehaviorSubject<boolean>(true);

  ngOnInit(): void {
    this.loadResources();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadResources(): void {
    this.loading$.next(true);
    this.resourceService.getAll().pipe(
      catchError(() => of([] as Resource[])),
      finalize(() => this.loading$.next(false)),
      takeUntil(this.destroy$),
    ).subscribe(resources => {
      const services = resources.filter(r => r.type?.toLowerCase() === 'service');
      this.totalCount = services.length;
      this.runningCount = services.filter(r => r.status === 'Running').length;
      this.stoppedCount = services.filter(r => r.status === 'Stopped').length;
      this.degradedCount = services.filter(
        r => r.status === 'Degraded' || r.healthStatus === 'Degraded' || r.healthStatus === 'Unhealthy'
      ).length;
    });
  }
}
