import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnDestroy,
  OnInit,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { ActivatedRoute, RouterModule } from "@angular/router";
import { Subject, takeUntil } from "rxjs";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { CriticalAlert } from "@core/models/critical-alert.model";
import { LabCriticalAlertService } from "@core/services/lab-critical-alert.service";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";

@Component({
  selector: "app-lab-critical-alert-detail",
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatSnackBarModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (alert) {
      <mat-card class="alert-detail-card">
        <mat-card-header>
          <mat-card-title>{{ alert.message }}</mat-card-title>
          <mat-card-subtitle
            >{{ alert.resultValue }} {{ alert.resultUnit }}</mat-card-subtitle
          >
        </mat-card-header>
        <mat-card-content>
          <p><strong>Trạng thái:</strong> {{ alert.status }}</p>
          <p>
            <strong>Phiếu:</strong> {{ alert.labOrderId | slice: 0 : 8 }}...
          </p>
        </mat-card-content>
        <mat-card-actions>
          @if (canAcknowledge()) {
            <button
              mat-stroked-button
              color="primary"
              (click)="acknowledge()"
              [disabled]="alert.status !== 'OPEN'"
            >
              Ghi nhận
            </button>
          }
          @if (canResolve()) {
            <button
              mat-button
              (click)="resolve()"
              [disabled]="alert.status === 'RESOLVED'"
            >
              Đánh dấu đã xử lý
            </button>
          }
        </mat-card-actions>
      </mat-card>
    }
  `,
  styles: [
    `
      .alert-detail-card {
        margin: 24px;
        border: 1px solid #eaeaea;
      }
    `,
  ],
})
export class LabCriticalAlertDetailComponent implements OnInit, OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  alert?: CriticalAlert;
  private alertId = "";

  canAcknowledge(): boolean {
    return this.permissions.has("lab.alert.acknowledge");
  }
  canResolve(): boolean {
    return this.permissions.has("lab.alert.resolve");
  }

  constructor(
    private readonly route: ActivatedRoute,
    private readonly alertService: LabCriticalAlertService,
    private readonly snackBar: MatSnackBar,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  ngOnInit(): void {
    this.alertId = this.route.snapshot.paramMap.get("id") ?? "";
    this.loadAlert();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  acknowledge(): void {
    if (!this.alert) {
      return;
    }

    this.alertService
      .acknowledgeCriticalAlert(this.alert.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.snackBar.open(
          this.i18n.t("labOrders.acknowledgeSuccess", "Đã ghi nhận cảnh báo"),
          this.i18n.t("common.close", "Đóng"),
          { duration: 2500 },
        );
        this.loadAlert();
      });
  }

  resolve(): void {
    if (!this.alert) {
      return;
    }

    this.alertService
      .resolveCriticalAlert(this.alert.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.snackBar.open(
          this.i18n.t("labOrders.resolveSuccess", "Đã xử lý cảnh báo"),
          this.i18n.t("common.close", "Đóng"),
          { duration: 2500 },
        );
        this.loadAlert();
      });
  }

  private loadAlert(): void {
    this.alertService
      .listCriticalAlerts()
      .pipe(takeUntil(this.destroy$))
      .subscribe((alerts) => {
        this.alert = alerts.find((item) => item.id === this.alertId);
        this.cdr.markForCheck();
      });
  }
}
