import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { LabCriticalAlertService } from '@core/services/lab-critical-alert.service';
import { LabCriticalAlertStreamService } from '@core/services/lab-critical-alert-stream.service';
import { CriticalAlert, CriticalAlertStatus } from '@core/models/critical-alert.model';

@Component({
  selector: 'app-lab-critical-alerts',
  standalone: true,
  imports: [CommonModule, RouterModule, MatButtonModule, MatCardModule, MatIconModule, MatSnackBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="lab-critical-alerts">
      <div class="header">
        <div>
          <h1>Hộp cảnh báo xét nghiệm</h1>
          <p class="subtitle">{{ unreadCount }} cảnh báo mới</p>
        </div>

        <div class="header-actions">
          <a mat-stroked-button routerLink="/lab/critical-alerts/rules">Quy tắc</a>
          <a mat-raised-button color="primary" routerLink="/lab">Phiếu xét nghiệm</a>
        </div>
      </div>

      <div class="filters" role="tablist" aria-label="Bộ lọc cảnh báo nghiêm trọng">
        @for (filter of filters; track filter.status) {
        <button mat-stroked-button type="button" (click)="selectFilter(filter.status)" [class.active]="selectedFilter === filter.status">
          {{ filter.label }}
        </button>
        }
      </div>

      @if (visibleAlerts.length === 0) {
      <mat-card>
        <mat-card-content>Không có cảnh báo nào trong bộ lọc này.</mat-card-content>
      </mat-card>
      }

      <div class="alert-list">
        @for (alert of visibleAlerts; track alert.id) {
        <mat-card class="alert-card">
          <mat-card-header>
            <mat-card-title>{{ alert.message }}</mat-card-title>
            <mat-card-subtitle>{{ alert.resultValue }} {{ alert.resultUnit }}</mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <p><strong>Phiếu:</strong> {{ alert.labOrderId | slice:0:8 }}...</p>
            <p><strong>Ngưỡng:</strong> {{ alert.thresholdValue }}</p>
            <p><strong>Trạng thái:</strong> {{ statusLabel(alert.status) }}</p>
          </mat-card-content>
          <mat-card-actions>
            <button mat-stroked-button color="primary" (click)="acknowledge(alert)" [disabled]="alert.status !== 'OPEN'">Ghi nhận</button>
            <a mat-button [routerLink]="['/lab/critical-alerts', alert.id]">Chi tiết</a>
          </mat-card-actions>
        </mat-card>
        }
      </div>
    </div>
  `,
  styles: [`
    .lab-critical-alerts { padding: 24px; display: flex; flex-direction: column; gap: 20px; }
    .header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
    .header-actions { display: flex; gap: 12px; flex-wrap: wrap; }
    .subtitle { color: #787774; margin-top: 4px; }
    .filters { display: flex; gap: 12px; flex-wrap: wrap; }
    .filters .active { border-color: #2F6B4A; color: #2F6B4A; }
    .alert-list { display: grid; gap: 16px; }
    .alert-card { border: 1px solid #EAEAEA; }
  `],
})
export class LabCriticalAlertsComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly snackBar = inject(MatSnackBar);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly filters: Array<{ status: CriticalAlertStatus | 'ALL'; label: string }> = [
    { status: 'ALL', label: 'Tất cả' },
    { status: 'OPEN', label: 'Mở' },
    { status: 'ACKNOWLEDGED', label: 'Đã ghi nhận' },
    { status: 'RESOLVED', label: 'Đã xử lý' },
  ];

  alerts: CriticalAlert[] = [];
  visibleAlerts: CriticalAlert[] = [];
  selectedFilter: CriticalAlertStatus | 'ALL' = 'OPEN';
  unreadCount = 0;
  private lastToastAlertId: string | null = null;

  constructor(
    private readonly alertService: LabCriticalAlertService,
    private readonly streamService: LabCriticalAlertStreamService,
  ) {}

  ngOnInit(): void {
    void this.streamService.connect();

    this.streamService.unreadCount$
      .pipe(takeUntil(this.destroy$))
      .subscribe((count) => {
        this.unreadCount = count;
        this.cdr.markForCheck();
      });

    let firstLatestAlert = true;

    this.streamService.latestAlert$
      .pipe(takeUntil(this.destroy$))
      .subscribe((alert) => {
        if (firstLatestAlert) {
          firstLatestAlert = false;
          if (alert) {
            this.lastToastAlertId = alert.id;
          }
          return;
        }

        if (!alert || alert.id === this.lastToastAlertId) {
          return;
        }

        this.lastToastAlertId = alert.id;
        this.notify('Có cảnh báo xét nghiệm nghiêm trọng mới');
      });

    this.alertService.listCriticalAlerts()
      .pipe(takeUntil(this.destroy$))
      .subscribe((alerts) => {
        this.alerts = alerts;
        this.applyFilter();
        this.cdr.markForCheck();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    void this.streamService.disconnect();
  }

  selectFilter(status: CriticalAlertStatus | 'ALL'): void {
    this.selectedFilter = status;
    this.applyFilter();
  }

  acknowledge(alert: CriticalAlert): void {
    this.alertService.acknowledgeCriticalAlert(alert.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.notify('Đã ghi nhận cảnh báo nghiêm trọng');
          this.streamService.markAllRead();
          this.refreshAlerts();
        },
      });
  }

  statusLabel(status: CriticalAlertStatus): string {
    return status === 'OPEN' ? 'Mở' : status === 'ACKNOWLEDGED' ? 'Đã ghi nhận' : 'Đã xử lý';
  }

  private refreshAlerts(): void {
    this.alertService.listCriticalAlerts()
      .pipe(takeUntil(this.destroy$))
      .subscribe((alerts) => {
        this.alerts = alerts;
        this.applyFilter();
        this.cdr.markForCheck();
      });
  }

  private applyFilter(): void {
    this.visibleAlerts = this.selectedFilter === 'ALL'
      ? this.alerts
      : this.alerts.filter((alert) => alert.status === this.selectedFilter);
  }

  private notify(message: string): void {
    this.snackBar.open(message, 'Đóng', { duration: 2500 });
  }
}
