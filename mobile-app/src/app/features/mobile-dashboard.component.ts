import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeMobileAccordionComponent,
  HisHopeMobileAvatarComponent,
  HisHopeMobileIconComponent,
  HisHopeMobileListComponent,
  HisHopeMobileListItemComponent,
  HisHopeMobileSegmentComponent,
  HisHopeStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import { DashboardStats } from "../core/contracts/mobile.contracts";
import { DashboardApiService } from "../core/services/dashboard-api.service";
import { MobileResourceStateController } from "../core/services/mobile-resource-state.controller";

@Component({
  standalone: true,
  imports: [
    RouterLink,
    HisHopeMobileAccordionComponent,
    HisHopeMobileAvatarComponent,
    HisHopeMobileIconComponent,
    HisHopeMobileListComponent,
    HisHopeMobileListItemComponent,
    HisHopeMobileSegmentComponent,
    HisHopeStateComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="mobile-page">
      <hh-toolbar [label]="'mobile.controls' | hhTranslate"
        ><span hhToolbarTitle>{{ "mobile.overview" | hhTranslate }}</span
        ><button
          hh-toolbar-actions
          class="hh-icon-button"
          type="button"
          (click)="loadDashboardStats()"
          [attr.aria-label]="'mobile.refreshDashboard' | hhTranslate"
        >
          <hh-mobile-icon name="refresh" /></button
      ></hh-toolbar>
      @if (loading) {
        <hh-state
          kind="loading"
          [message]="'mobile.loadingDashboard' | hhTranslate"
        />
      } @else if (error) {
        <hh-state kind="error" [message]="error"
          ><button
            class="hh-button hh-button--secondary"
            type="button"
            (click)="loadDashboardStats()"
          >
            {{ "common.retry" | hhTranslate }}
          </button></hh-state
        >
      } @else if (stats) {
        <section class="welcome-panel" aria-labelledby="welcome-title">
          <div class="welcome-panel__copy">
            <hh-mobile-avatar
              initials="AD"
              [label]="'mobile.administrator' | hhTranslate"
            />
            <div>
              <p class="eyebrow">
                {{ "admin.identityAdministration" | hhTranslate }}
              </p>
              <h1 id="welcome-title">
                {{ "mobile.goodToSeeYou" | hhTranslate }}
              </h1>
              <p>{{ "mobile.manageWorkspace" | hhTranslate }}</p>
            </div>
          </div>
          <hh-mobile-icon class="welcome-icon" name="security" size="large" />
        </section>
        <hh-mobile-segment
          [label]="'mobile.dashboardView' | hhTranslate"
          [options]="dashboardViews"
          [value]="dashboardView"
          (valueChange)="dashboardView = $event"
        />
        @if (dashboardView === "overview") {
          <div
            class="stats-grid"
            [attr.aria-label]="'mobile.administrationSummary' | hhTranslate"
          >
            <a class="stat-card" routerLink="/admin/clients"
              ><hh-mobile-icon name="clients" size="medium" />
              <div class="stat-card__metric"
                ><strong>{{ stats.totalClients }}</strong
                ><span>{{ "admin.clients" | hhTranslate }}</span></div
              ></a
            >
            <a class="stat-card" routerLink="/admin/users"
              ><hh-mobile-icon name="users" size="medium" />
              <div class="stat-card__metric"
                ><strong>{{ stats.totalUsers }}</strong
                ><span>{{ "admin.users" | hhTranslate }}</span></div
              ></a
            >
            <a class="stat-card" routerLink="/admin/roles"
              ><hh-mobile-icon name="roles" size="medium" />
              <div class="stat-card__metric"
                ><strong>{{ stats.totalRoles }}</strong
                ><span>{{ "admin.roles" | hhTranslate }}</span></div
              ></a
            >
            <a class="stat-card" routerLink="/admin/consents"
              ><hh-mobile-icon name="consents" size="medium" />
              <div class="stat-card__metric"
                ><strong>{{ stats.totalConsents }}</strong
                ><span>{{ "admin.consents" | hhTranslate }}</span></div
              ></a
            >
          </div>
          <section
            class="quick-actions"
            [attr.aria-labelledby]="'quick-actions-title'"
          >
            <div class="section-heading">
              <h2 id="quick-actions-title">
                {{ "mobile.quickAccess" | hhTranslate }}
              </h2>
              <span>{{ "mobile.commonTasks" | hhTranslate }}</span>
            </div>
            <hh-mobile-list [label]="'mobile.quickAccess' | hhTranslate"
              ><hh-mobile-list-item
                variant="action"
                (activated)="navigate('/admin/clients')"
                ><hh-mobile-icon
                  hhMobileItemLeading
                  class="action-icon"
                  name="clients" /><span hhMobileItemTitle>{{
                  "admin.manageClients" | hhTranslate
                }}</span
                ><span hhMobileItemDescription>{{
                  "mobile.oidcRedirects" | hhTranslate
                }}</span
                ><hh-mobile-icon
                  hhMobileItemTrailing
                  class="arrow"
                  name="next" /></hh-mobile-list-item
              ><hh-mobile-list-item
                variant="action"
                (activated)="navigate('/admin/users')"
                ><hh-mobile-icon
                  hhMobileItemLeading
                  class="action-icon"
                  name="users" /><span hhMobileItemTitle>{{
                  "admin.manageUsers" | hhTranslate
                }}</span
                ><span hhMobileItemDescription>{{
                  "mobile.accountsRoles" | hhTranslate
                }}</span
                ><hh-mobile-icon
                  hhMobileItemTrailing
                  class="arrow"
                  name="next" /></hh-mobile-list-item
              ><hh-mobile-list-item
                variant="action"
                (activated)="navigate('/admin/consents')"
                ><hh-mobile-icon
                  hhMobileItemLeading
                  class="action-icon"
                  name="consents" /><span hhMobileItemTitle>{{
                  "admin.reviewConsents" | hhTranslate
                }}</span
                ><span hhMobileItemDescription>{{
                  "mobile.recentApprovals" | hhTranslate
                }}</span
                ><hh-mobile-icon
                  hhMobileItemTrailing
                  class="arrow"
                  name="next" /></hh-mobile-list-item
            ></hh-mobile-list>
          </section>
        } @else {
          <section class="security-panel">
            <h2>{{ "mobile.securityPosture" | hhTranslate }}</h2>
            <hh-mobile-accordion
              [title]="'mobile.protectedAdministration' | hhTranslate"
              ><p>
                {{ "mobile.permissionAware" | hhTranslate }}
              </p></hh-mobile-accordion
            ><hh-mobile-accordion [title]="'mobile.multiFactor' | hhTranslate"
              ><p>{{ "mobile.authenticatorProtection" | hhTranslate }}</p>
              <a routerLink="/admin/mfa" class="security-link">{{
                "mobile.configureMfa" | hhTranslate
              }}</a></hh-mobile-accordion
            ><hh-mobile-accordion [title]="'mobile.recentReview' | hhTranslate"
              ><p>
                {{ "mobile.auditReview" | hhTranslate }}
              </p></hh-mobile-accordion
            >
          </section>
        }
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .mobile-page {
        display: grid;
        gap: var(--size-timeline-dot);
      }
      .welcome-panel {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--space-md);
        padding: var(--font-size-body) var(--space-lg);
        border: 1px solid
          color-mix(in srgb, var(--color-primary) 20%, var(--border-default));
        border-radius: var(--radius-card);
        background: linear-gradient(
          135deg,
          color-mix(
            in srgb,
            var(--color-primary-soft) 82%,
            var(--surface-white)
          ),
          var(--surface-white)
        );
      }
      .welcome-panel__copy {
        display: flex;
        align-items: center;
        gap: var(--space-inset);
        min-width: 0;
      }
      .welcome-panel h1 {
        margin: var(--space-xxs) 0 var(--space-2xs);
        font-size: var(--font-size-headline);
        letter-spacing: -0.01em;
      }
      .welcome-panel p:not(.eyebrow) {
        max-width: 34ch;
        margin: 0;
        color: var(--text-secondary);
        font-size: var(--font-size-label);
        line-height: 1.35;
      }
      .eyebrow {
        margin: 0;
        color: var(--color-primary);
        font-size: var(--font-size-overline);
        font-weight: var(--font-weight-semibold);
        letter-spacing: 0.08em;
      }
      .welcome-icon {
        flex: 0 0 44px;
        width: var(--touch-target);
        height: var(--touch-target);
        border-radius: var(--radius-feature);
        background: var(--surface-white);
        color: var(--color-primary);
        box-shadow: var(--shadow-welcome-icon);
      }
      .stats-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--space-inset);
      }
      .stat-card {
        display: grid;
        gap: var(--space-xs);
        min-height: 104px;
        padding: var(--space-md);
        box-sizing: border-box;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
        color: var(--text-primary);
        text-decoration: none;
        transition:
          transform 0.16s ease,
          border-color 0.16s ease,
          box-shadow 0.16s ease;
      }
      .stat-card:active {
        transform: scale(0.98);
      }
      .stat-card hh-mobile-icon {
        width: var(--space-3xl);
        height: var(--space-3xl);
        padding: var(--space-compact);
        box-sizing: border-box;
        border-radius: var(--radius-chip);
        background: var(--color-primary-soft);
        color: var(--color-primary);
      }
      .stat-card strong {
        font-size: var(--font-size-title);
        line-height: 1;
      }
      .stat-card__metric {
        display: flex;
        align-items: baseline;
        gap: var(--space-xs);
        min-width: 0;
      }
      .stat-card__metric span {
        color: var(--text-secondary);
        font-size: var(--font-size-label);
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
      .quick-actions,
      .security-panel {
        display: grid;
        gap: 0;
        padding: var(--size-timeline-dot);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
      }
      .section-heading {
        display: flex;
        align-items: baseline;
        justify-content: space-between;
        gap: var(--space-md);
        margin-bottom: var(--space-2xs);
      }
      h2 {
        margin: 0;
        font-size: var(--font-size-subhead);
      }
      .section-heading span {
        color: var(--text-secondary);
        font-size: var(--font-size-nav);
      }
      .action-icon {
        width: var(--space-3xl) !important;
        height: var(--space-3xl) !important;
        padding: var(--space-compact);
        box-sizing: border-box;
        border-radius: var(--radius-chip);
        background: var(--color-primary-soft);
        color: var(--color-primary);
      }
      .arrow {
        color: var(--text-secondary);
        font-size: var(--font-size-section);
      }
      .security-link {
        display: inline-block;
        margin-top: var(--space-sm);
        color: var(--color-primary);
        font-weight: var(--font-weight-semibold);
      }
    `,
  ],
})
export class MobileDashboardComponent implements OnInit {
  private readonly api = inject(DashboardApiService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly destroyRef = inject(DestroyRef);

  stats: DashboardStats | null = null;
  dashboardView = "overview";
  dashboardViews: Array<{ value: string; label: string }> = [];

  readonly state = new MobileResourceStateController<DashboardStats>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "mobile.dashboardLoadFailed",
    loadErrorFallback: "Unable to load dashboard.",
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
    effect(() => {
      this.i18n.locale();
      this.dashboardViews = [
        {
          value: "overview",
          label: this.i18n.t("mobile.overview", "Overview"),
        },
        {
          value: "security",
          label: this.i18n.t("mobile.securityPosture", "Security"),
        },
      ];
      this.cdr.markForCheck();
    });
  }

  ngOnInit(): void {
    this.loadDashboardStats();
  }

  navigate(url: string): void {
    void this.router.navigateByUrl(url);
  }

  loadDashboardStats(): void {
    this.state.load(this.api.getDashboardStats());
  }
}
