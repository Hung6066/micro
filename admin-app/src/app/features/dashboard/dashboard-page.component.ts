import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { DashboardStats } from "../../core/contracts/admin.contracts";
import { DashboardApiService } from "../../core/services/dashboard-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  HisHopeMetricCardComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-dashboard-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    MatButtonModule,
    HisHopeMetricCardComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.dashboardTitle' | hhTranslate"
        [subtitle]="'admin.dashboardSubtitle' | hhTranslate"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'admin.loadingDashboard' | hhTranslate"
        />
      } @else if (error) {
        <hh-state kind="error" icon="error" [message]="error">
          <hh-action-button
            (pressed)="loadDashboardStats()"
            kind="secondary"
            icon="refresh"
            [label]="'common.retry' | hhTranslate"
          />
        </hh-state>
      } @else if (stats) {
        <div class="stats-grid">
          <hh-metric-card
            icon="vpn_key"
            [label]="'admin.clients' | hhTranslate"
            [value]="stats.totalClients"
            link="/clients"
            [action]="'admin.manageClients' | hhTranslate"
          />
          <hh-metric-card
            icon="people"
            [label]="'admin.users' | hhTranslate"
            [value]="stats.totalUsers"
            link="/users"
            [action]="'admin.manageUsers' | hhTranslate"
          />
          <hh-metric-card
            icon="badge"
            [label]="'admin.roles' | hhTranslate"
            [value]="stats.totalRoles"
            link="/roles"
            [action]="'admin.manageRoles' | hhTranslate"
            tone="info"
          />
          <hh-metric-card
            icon="checklist"
            [label]="'admin.consents' | hhTranslate"
            [value]="stats.totalConsents"
            link="/consents"
            [action]="'admin.reviewConsents' | hhTranslate"
            tone="warning"
          />
        </div>
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
    `,
  ],
})
export class DashboardPageComponent implements OnInit {
  private readonly api = inject(DashboardApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly cdr = inject(ChangeDetectorRef);
  stats: DashboardStats | null = null;
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<DashboardStats>({
    destroyRef: this.destroyRef,
    loadErrorMessageKey: "admin.dashboardLoadFailed",
    loadErrorFallback: "Unable to load dashboard statistics.",
  });
  get loading(): boolean {
    return this.state.loading;
  }
  get error(): string {
    return this.state.error;
  }

  constructor() {
    effect(() => {
      this.stats = this.state.resource.data();
      this.cdr.markForCheck();
    });
  }

  ngOnInit(): void {
    this.loadDashboardStats();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.loadDashboardStats());
  }

  loadDashboardStats(): void {
    this.state.load(
      this.api.getDashboardStats(),
    );
  }
}
