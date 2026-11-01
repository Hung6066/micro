import { HttpClient } from "@angular/common/http";
import { DecimalPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { forkJoin, of } from "rxjs";
import { catchError } from "rxjs/operators";
import {
  HisHopeActionButtonComponent,
  HisHopeMetricCardComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeManufacturingDashboardSummaryDto,
  HisHopeManufacturingProductionKpiDto,
  HisHopeManufacturingMachineHealthDto,
  HisHopeManufacturingProductionCostDto,
  HisHopeManufacturingOeeDto,
  HisHopeManufacturingExecutiveExceptionDto,
  HisHopeCostProjectionDto,
} from "@his-hope/frontend-foundation/contracts";
import { environment } from "../../../environments/environment";
import { ManufacturingApiService } from "../../core/services/manufacturing-api.service";
import { HisHopeApiErrorMessageService as ApiErrorMessageService } from "@his-hope/frontend-foundation/i18n";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { portalEnumLabel } from "../../core/utils/portal-label.util";

interface DashboardStats {
  totalUsers: number;
  activeUsers: number;
  totalRoles: number;
  totalClients: number;
}

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    DecimalPipe,
    HisHopeActionButtonComponent,
    HisHopeMetricCardComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'customerPortal.dashboardTitle' | hhTranslate: 'Dashboard'"
        [subtitle]="'customerPortal.dashboardSubtitle' | hhTranslate: 'Your tenant overview'"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'customerPortal.loadingDashboard' | hhTranslate: 'Loading dashboard…'"
        />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error">
          <hh-action-button
            (pressed)="loadDashboardStats()"
            kind="secondary"
            icon="refresh"
            [label]="'common.retry' | hhTranslate: 'Retry'"
          />
        </hh-state>
      } @else if (stats) {
        <div class="stats-grid">
          <hh-metric-card
            icon="people"
            [label]="'customerPortal.users' | hhTranslate: 'Users'"
            [value]="stats.totalUsers"
            link="/users"
            [action]="'customerPortal.manageUsers' | hhTranslate: 'Manage users'"
          />
          <hh-metric-card
            icon="person"
            [label]="'admin.active' | hhTranslate: 'Active'"
            [value]="stats.activeUsers"
            link="/users"
            [action]="'customerPortal.manageUsers' | hhTranslate: 'Manage users'"
            tone="success"
          />
          <hh-metric-card
            icon="badge"
            [label]="'admin.roles' | hhTranslate: 'Roles'"
            [value]="stats.totalRoles"
            link="/dashboard"
            tone="info"
          />
          <hh-metric-card
            icon="vpn_key"
            [label]="'admin.clients' | hhTranslate: 'Clients'"
            [value]="stats.totalClients"
            link="/dashboard"
            tone="warning"
          />
        </div>
        @if (mfgSummary) {
          <h2 class="section-title">
            {{ "customerPortal.manufacturingSection" | hhTranslate: "Manufacturing" }}
          </h2>
          <div class="stats-grid mfg-grid">
            <hh-metric-card
              icon="inventory_2"
              [label]="'customerPortal.manufacturingLots' | hhTranslate: 'Lots'"
              [value]="mfgSummary.lotCount"
              link="/inventory/lots"
              [action]="'customerPortal.viewInventory' | hhTranslate: 'View inventory'"
            />
            <hh-metric-card
              icon="check_circle"
              [label]="'customerPortal.manufacturingReleasedQty' | hhTranslate: 'Released quantity'"
              [value]="mfgSummary.releasedQuantity"
              link="/inventory/lots"
              tone="success"
            />
            <hh-metric-card
              icon="precision_manufacturing"
              [label]="'customerPortal.manufacturingOpenOrders' | hhTranslate: 'Open orders'"
              [value]="mfgSummary.openProductionOrderCount"
              link="/production"
              [action]="'customerPortal.production' | hhTranslate: 'Production'"
              tone="info"
            />
            <hh-metric-card
              icon="play_circle"
              [label]="'customerPortal.manufacturingActiveBatches' | hhTranslate: 'Active batches'"
              [value]="mfgSummary.activeBatchCount"
              link="/production"
              tone="warning"
            />
          </div>
          @if (mfgKpi) {
            <div class="stats-grid mfg-grid">
              <hh-metric-card
                icon="output"
                [label]="'customerPortal.manufacturingActualOutput' | hhTranslate: 'Actual output'"
                [value]="mfgKpi.actualOutputQuantity"
                link="/production"
                tone="success"
              />
              <hh-metric-card
                icon="trending_down"
                [label]="'customerPortal.manufacturingTotalLoss' | hhTranslate: 'Total loss'"
                [value]="mfgKpi.totalLossQuantity"
                link="/production"
                tone="warning"
              />
              <hh-metric-card
                icon="percent"
                [label]="'customerPortal.manufacturingAverageYield' | hhTranslate: 'Average yield'"
                [value]="mfgKpi.averageYieldPercent"
                link="/production"
                tone="info"
              />
              <hh-metric-card
                icon="compare_arrows"
                [label]="'customerPortal.manufacturingYieldVariance' | hhTranslate: 'Yield variance'"
                [value]="mfgKpi.yieldVariancePercent"
                link="/production"
                tone="info"
              />
            </div>
          }
          @if (mfgMachineHealth) {
            <div class="stats-grid mfg-grid">
              <hh-metric-card
                icon="build"
                [label]="'customerPortal.manufacturingMaintenanceDue' | hhTranslate: 'Maintenance due soon'"
                [value]="mfgMachineHealth.dueWithinDaysCount"
                link="/production"
                tone="warning"
              />
              <hh-metric-card
                icon="check_circle"
                [label]="'customerPortal.manufacturingAvailableMachines' | hhTranslate: 'Available machines'"
                [value]="mfgMachineHealth.availableMachineCount"
                link="/production"
                tone="success"
              />
            </div>
          }
          @if (mfgOee) {
            <div class="stats-grid mfg-grid">
              <hh-metric-card
                icon="speed"
                [label]="'customerPortal.manufacturingOeeStatus' | hhTranslate: 'OEE data status'"
                [value]="mfgOee.status"
                link="/maintenance"
                [action]="'customerPortal.viewTelemetry' | hhTranslate: 'View telemetry'"
                [tone]="mfgOee.status === 'complete' ? 'success' : 'warning'"
              />
              <hh-metric-card
                icon="timer"
                [label]="'customerPortal.manufacturingRunMinutes' | hhTranslate: 'Run minutes'"
                [value]="mfgOee.runMinutes"
                link="/maintenance"
                tone="info"
              />
            </div>
          }
          @if (mfgCost) {
            <div class="stats-grid mfg-grid">
              <hh-metric-card
                icon="payments"
                [label]="'customerPortal.manufacturingCostPerUnit' | hhTranslate: 'Estimated cost / output unit'"
                [value]="mfgCost.estimatedCostPerOutputUnit"
                link="/production"
                tone="info"
              />
              <hh-metric-card
                icon="warning"
                [label]="'customerPortal.manufacturingMissingPrices' | hhTranslate: 'Missing material prices'"
                [value]="mfgCost.missingPriceSkus.length"
                link="/procurement"
                tone="warning"
              />
            </div>
          }
          <section class="projection-panel">
            <div class="section-heading"><h2>{{ "customerPortal.costProjection" | hhTranslate: "Cost projection" }}</h2></div>
            <form class="projection-form" (ngSubmit)="projectCost()">
              <input name="projectionSku" [(ngModel)]="projectionSku" [placeholder]="'customerPortal.productSku' | hhTranslate: 'Product SKU'" required />
              <input name="projectionQuantity" type="number" min="0.001" step="0.001" [(ngModel)]="projectionQuantity" [placeholder]="'customerPortal.plannedQuantity' | hhTranslate: 'Planned output quantity'" required />
              <hh-action-button kind="secondary" icon="calculate" type="submit" [label]="'customerPortal.calculateProjection' | hhTranslate: 'Calculate'" [disabled]="projectionBusy" />
            </form>
            @if (projectionError) { <p class="error">{{ projectionError }}</p> }
            @if (projection) { <div class="projection-result"><strong>{{ projection.productSku }}</strong><span>{{ projection.estimatedMaterialCost | number:'1.0-2' }} · {{ projection.estimatedMaterialCostPerOutputUnit | number:'1.0-4' }} / {{ projection.outputUom }}</span><small>{{ "customerPortal.projectedLoss" | hhTranslate: "Projected loss" }}: {{ projection.projectedLossQuantity | number:'1.0-2' }}</small></div> }
          </section>
          @if (mfgExceptions) {
            <div class="stats-grid mfg-grid">
              <hh-metric-card
                icon="warning"
                [label]="'customerPortal.manufacturingExceptions' | hhTranslate: 'Executive exceptions'"
                [value]="mfgExceptions.length"
                link="/dashboard"
                tone="warning"
              />
            </div>
            @if (mfgExceptions.length > 0) {
              <section class="exception-list" [attr.aria-label]="'customerPortal.manufacturingExceptions' | hhTranslate: 'Executive exceptions'">
                @for (exception of mfgExceptions; track exception.code + exception.entityId) {
                  <article class="exception-item" [class.high]="exception.severity === 'High'">
                    <div class="exception-heading">
                      <strong>{{ exception.title }}</strong>
                      <span>{{ severityLabel(exception.severity) }}</span>
                    </div>
                    <p>{{ exception.detail }}</p>
                  </article>
                }
              </section>
            }
          }
        }
      }
    </hh-page-layout>
  `,
  styles: [
    `
      .stats-grid {
        display: grid;
        grid-template-columns: repeat(4, minmax(0, 1fr));
        gap: var(--space-lg);
      }
      .projection-panel { margin-top: var(--space-lg); padding: var(--space-md); border: 1px solid var(--border-subtle); border-radius: var(--radius-card); }
      .section-heading { display:flex; justify-content:space-between; align-items:center; }
      .projection-form { display:flex; gap:var(--space-sm); flex-wrap:wrap; align-items:center; }
      .projection-form input { min-height:var(--control-height); padding:0 var(--space-sm); border:1px solid var(--border-subtle); border-radius:var(--radius-control); background:var(--surface-raised); color:var(--text-primary); font:inherit; }
      .projection-result { display:grid; gap:var(--space-xs); margin-top:var(--space-md); color:var(--text-primary); }
      .projection-result small,.error { color:var(--text-secondary); }
      @media (max-width: 900px) {
        .stats-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
      @media (max-width: 600px) {
        .stats-grid {
          grid-template-columns: 1fr;
        }
      }
      .section-title {
        margin: var(--space-xl) 0 var(--space-md);
        font-size: var(--font-size-section);
        font-weight: var(--font-weight-semibold);
        font-family: var(--font-sans);
      }
      .mfg-grid {
        margin-bottom: var(--space-lg);
      }
      .exception-list {
        display: grid;
        gap: var(--space-sm);
        margin: 0 0 var(--space-lg);
      }
      .exception-item {
        border: 1px solid var(--border-default);
        border-left: 4px solid var(--color-warning);
        border-radius: var(--radius-card);
        padding: var(--space-md);
        background: var(--surface-white);
      }
      .exception-item.high {
        border-left-color: var(--color-danger);
      }
      .exception-heading {
        display: flex;
        justify-content: space-between;
        gap: var(--space-md);
      }
      .exception-heading span {
        font-size: var(--font-size-caption);
        color: var(--text-secondary);
      }
      .exception-item p {
        margin: var(--space-xs) 0 0;
        color: var(--text-secondary);
      }
    `,
  ],
})
export class DashboardPageComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly manufacturingApi = inject(ManufacturingApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly errors = inject(ApiErrorMessageService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly tenantContext = inject(TenantContextService);
  stats: DashboardStats | null = null;
  mfgSummary: HisHopeManufacturingDashboardSummaryDto | null = null;
  mfgKpi: HisHopeManufacturingProductionKpiDto | null = null;
  mfgMachineHealth: HisHopeManufacturingMachineHealthDto | null = null;
  mfgOee: HisHopeManufacturingOeeDto | null = null;
  mfgCost: HisHopeManufacturingProductionCostDto | null = null;
  mfgExceptions: HisHopeManufacturingExecutiveExceptionDto[] | null = null;
  projectionSku = "";
  projectionQuantity = 0;
  projectionBusy = false;
  projectionError = "";
  projection: HisHopeCostProjectionDto | null = null;
  loading = true;
  error = "";

  severityLabel(severity: string): string {
    return portalEnumLabel(this.i18n, "severity", severity);
  }

  projectCost(): void {
    if (!this.projectionSku.trim() || this.projectionQuantity <= 0) {
      this.projectionError = this.i18n.t("customerPortal.projectionFormInvalid", "Product SKU and a positive quantity are required.");
      return;
    }
    this.projectionBusy = true;
    this.projectionError = "";
    this.manufacturingApi.getCostProjection(this.projectionSku.trim(), this.projectionQuantity).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => { this.projection = result; this.projectionBusy = false; this.cdr.markForCheck(); },
      error: (error) => { this.projectionError = this.errors.message(error, "customerPortal.projectionLoadFailed"); this.projectionBusy = false; this.cdr.markForCheck(); },
    });
  }

  ngOnInit(): void {
    this.loadDashboardStats();
    this.tenantContext.activeTenantKey$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadDashboardStats());
  }

  loadDashboardStats(): void {
    this.loading = true;
    this.error = "";
    const admin$ = this.http.get<DashboardStats>(`${environment.adminApiUrl}/dashboard`);
    const mfg$ = this.manufacturingApi.isEnabled()
      ? this.manufacturingApi.getDashboardSummary().pipe(catchError(() => of(null)))
      : of(null);
    const mfgKpi$ = this.manufacturingApi.isEnabled()
      ? this.manufacturingApi.getProductionKpis().pipe(catchError(() => of(null)))
      : of(null);
    const mfgMachineHealth$ = this.manufacturingApi.isEnabled()
      ? this.manufacturingApi.getMachineHealth().pipe(catchError(() => of(null)))
      : of(null);
    const mfgOee$ = this.manufacturingApi.isEnabled()
      ? this.manufacturingApi.getOee().pipe(catchError(() => of(null)))
      : of(null);
    const mfgCost$ = this.manufacturingApi.isEnabled()
      ? this.manufacturingApi.getProductionCosts().pipe(catchError(() => of(null)))
      : of(null);
    const mfgExceptions$ = this.manufacturingApi.isEnabled()
      ? this.manufacturingApi.getExecutiveExceptions().pipe(catchError(() => of(null)))
      : of(null);

    forkJoin({ stats: admin$, mfg: mfg$, mfgKpi: mfgKpi$, mfgMachineHealth: mfgMachineHealth$, mfgOee: mfgOee$, mfgCost: mfgCost$, mfgExceptions: mfgExceptions$ })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ stats, mfg, mfgKpi, mfgMachineHealth, mfgOee, mfgCost, mfgExceptions }) => {
          this.stats = stats;
          this.mfgSummary = mfg;
          this.mfgKpi = mfgKpi;
          this.mfgMachineHealth = mfgMachineHealth;
          this.mfgOee = mfgOee;
          this.mfgCost = mfgCost;
          this.mfgExceptions = mfgExceptions;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: (error) => {
          this.stats = null;
          this.mfgSummary = null;
          this.mfgKpi = null;
          this.mfgMachineHealth = null;
          this.mfgOee = null;
          this.mfgCost = null;
          this.mfgExceptions = null;
          this.loading = false;
          this.error = this.errors.message(error, "customerPortal.dashboardLoadFailed");
          this.cdr.markForCheck();
        },
      });
  }
}
