import { Injectable, OnDestroy } from '@angular/core';
import { MatSnackBar, MatSnackBarRef, SimpleSnackBar } from '@angular/material/snack-bar';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { AlertService } from '../../core/services/alert.service';
import { HisHopeI18nService } from '@his-hope/frontend-foundation';

@Injectable({ providedIn: 'root' })
export class AlertToastService implements OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private activeToastRef: MatSnackBarRef<SimpleSnackBar> | null = null;

  constructor(
    private readonly snackBar: MatSnackBar,
    private readonly alertService: AlertService,
    private readonly i18n: HisHopeI18nService,
  ) {
    this.alertService.newCriticalAlert$
      .pipe(takeUntil(this.destroy$))
      .subscribe(alert => this.showAlertToast(alert));
  }

  private showAlertToast(alert: { severity: string; summary: string; service: string }): void {
    // Dismiss previous toast if still visible
    this.activeToastRef?.dismiss();

    const isCritical = alert.severity === 'critical';
    const panelClass = isCritical ? 'toast-critical' : 'toast-warning';
    const title = isCritical
      ? this.i18n.t('dashboard.alerts.criticalAlert', 'Cảnh báo nghiêm trọng')
      : this.i18n.t('dashboard.alerts.warningAlert', 'Cảnh báo');

    this.activeToastRef = this.snackBar.open(
      `🔔 ${title}: ${alert.summary} (${alert.service})`,
      this.i18n.t('dashboard.alerts.view', 'Xem'),
      {
        duration: 10_000,
        panelClass: [panelClass],
        horizontalPosition: 'end',
        verticalPosition: 'top',
      },
    );

    // When "View" is clicked, we could navigate to alert panel
    this.activeToastRef.onAction().pipe(takeUntil(this.destroy$)).subscribe(() => {
      // The alert panel opens via toolbar button — no navigation needed
      this.activeToastRef = null;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.activeToastRef?.dismiss();
  }
}
