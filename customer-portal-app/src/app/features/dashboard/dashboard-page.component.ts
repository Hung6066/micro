import { HttpClient } from "@angular/common/http";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import {
  HisHopeActionButtonComponent,
  HisHopeMetricCardComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeTranslatePipe } from "@his-hope/frontend-foundation/i18n";
import { environment } from "../../../environments/environment";

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
    MatButtonModule,
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
        [title]="'customerPortal.dashboardTitle' | hhTranslate"
        [subtitle]="'customerPortal.dashboardSubtitle' | hhTranslate"
      />
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'customerPortal.loadingDashboard' | hhTranslate"
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
            icon="people"
            [label]="'customerPortal.users' | hhTranslate"
            [value]="stats.totalUsers"
            link="/users"
            [action]="'customerPortal.manageUsers' | hhTranslate"
          />
          <hh-metric-card
            icon="person"
            [label]="'admin.active' | hhTranslate"
            [value]="stats.activeUsers"
            link="/users"
            [action]="'customerPortal.manageUsers' | hhTranslate"
            tone="success"
          />
          <hh-metric-card
            icon="badge"
            [label]="'admin.roles' | hhTranslate"
            [value]="stats.totalRoles"
            link="/dashboard"
            tone="info"
          />
          <hh-metric-card
            icon="vpn_key"
            [label]="'admin.clients' | hhTranslate"
            [value]="stats.totalClients"
            link="/dashboard"
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
  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  stats: DashboardStats | null = null;
  loading = true;
  error = "";

  ngOnInit(): void {
    this.loadDashboardStats();
  }

  loadDashboardStats(): void {
    this.loading = true;
    this.error = "";
    this.http
      .get<DashboardStats>(`${environment.adminApiUrl}/dashboard`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (value) => {
          this.stats = value;
          this.loading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.stats = null;
          this.loading = false;
          this.error = "Unable to load dashboard statistics.";
          this.cdr.markForCheck();
        },
      });
  }
}
