import { ChangeDetectorRef, Component, OnInit, inject } from "@angular/core";
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
import { catchError, finalize, of } from "rxjs";
import {
  MobileAdminApiService,
  MobileDashboardStats,
} from "../core/admin-api.service";

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
  template: `
    <section class="mobile-page">
      <hh-toolbar [label]="'mobile.controls' | hhTranslate"
        ><span hhToolbarTitle>{{ "mobile.overview" | hhTranslate }}</span
        ><button
          hh-toolbar-actions
          class="hh-icon-button"
          type="button"
          (click)="load()"
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
            (click)="load()"
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
              ><hh-mobile-icon name="clients" size="medium" /><strong>{{
                stats.totalClients
              }}</strong
              ><span>{{ "admin.clients" | hhTranslate }}</span></a
            >
            <a class="stat-card" routerLink="/admin/users"
              ><hh-mobile-icon name="users" size="medium" /><strong>{{
                stats.totalUsers
              }}</strong
              ><span>{{ "admin.users" | hhTranslate }}</span></a
            >
            <a class="stat-card" routerLink="/admin/roles"
              ><hh-mobile-icon name="roles" size="medium" /><strong>{{
                stats.totalRoles
              }}</strong
              ><span>{{ "admin.roles" | hhTranslate }}</span></a
            >
            <a class="stat-card" routerLink="/admin/consents"
              ><hh-mobile-icon name="consents" size="medium" /><strong>{{
                stats.totalConsents
              }}</strong
              ><span>{{ "admin.consents" | hhTranslate }}</span></a
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
        gap: 14px;
      }
      .welcome-panel {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        padding: 14px 16px;
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
        gap: 10px;
        min-width: 0;
      }
      .welcome-panel h1 {
        margin: 3px 0 4px;
        font-size: 22px;
        letter-spacing: -0.01em;
      }
      .welcome-panel p:not(.eyebrow) {
        max-width: 34ch;
        margin: 0;
        color: var(--text-secondary);
        font-size: 13px;
        line-height: 1.35;
      }
      .eyebrow {
        margin: 0;
        color: var(--color-primary);
        font-size: 10px;
        font-weight: var(--font-weight-semibold);
        letter-spacing: 0.08em;
      }
      .welcome-icon {
        flex: 0 0 44px;
        width: 44px;
        height: 44px;
        border-radius: 14px;
        background: var(--surface-white);
        color: var(--color-primary);
        box-shadow: 0 6px 18px
          color-mix(in srgb, var(--color-primary) 14%, transparent);
      }
      .stats-grid {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 10px;
      }
      .stat-card {
        display: grid;
        gap: 6px;
        min-height: 104px;
        padding: 12px;
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
        width: 32px;
        height: 32px;
        padding: 7px;
        box-sizing: border-box;
        border-radius: 10px;
        background: var(--color-primary-soft);
        color: var(--color-primary);
      }
      .stat-card strong {
        font-size: 24px;
        line-height: 1;
      }
      .stat-card span:last-child {
        color: var(--text-secondary);
        font-size: 13px;
      }
      .quick-actions,
      .security-panel {
        display: grid;
        gap: 0;
        padding: 14px;
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-white);
      }
      .section-heading {
        display: flex;
        align-items: baseline;
        justify-content: space-between;
        gap: 12px;
        margin-bottom: 4px;
      }
      h2 {
        margin: 0;
        font-size: 17px;
      }
      .section-heading span {
        color: var(--text-secondary);
        font-size: 11px;
      }
      .quick-actions a {
        display: flex;
        align-items: center;
        gap: 10px;
        min-height: 60px;
        border-bottom: 1px solid var(--border-light);
        color: var(--text-primary);
        text-decoration: none;
      }
      .quick-actions a:last-child {
        border-bottom: 0;
      }
      .action-icon {
        width: 32px !important;
        height: 32px !important;
        padding: 7px;
        box-sizing: border-box;
        border-radius: 10px;
        background: var(--color-primary-soft);
        color: var(--color-primary);
      }
      .action-copy {
        display: grid;
        gap: 3px;
        flex: 1;
      }
      .action-copy strong {
        font-size: 14px;
      }
      .action-copy small {
        color: var(--text-secondary);
        font-size: 12px;
      }
      .arrow {
        color: var(--text-secondary);
        font-size: 20px;
      }
      .security-link {
        display: inline-block;
        margin-top: 8px;
        color: var(--color-primary);
        font-weight: var(--font-weight-semibold);
      }
    `,
  ],
})
export class MobileDashboardComponent implements OnInit {
  private readonly api = inject(MobileAdminApiService);
  private readonly router = inject(Router);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly i18n = inject(HisHopeI18nService);
  stats: MobileDashboardStats | null = null;
  loading = false;
  error = "";
  dashboardView = "overview";
  readonly dashboardViews = [
    { value: "overview", label: "Overview" },
    { value: "security", label: "Security" },
  ];
  ngOnInit(): void {
    this.load();
  }
  navigate(url: string): void {
    void this.router.navigateByUrl(url);
  }
  load(): void {
    this.loading = true;
    this.error = "";
    this.changeDetector.detectChanges();
    this.api
      .getDashboard()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.changeDetector.detectChanges();
        }),
        catchError(() => {
          this.error = this.i18n.t("mobile.dashboardLoadFailed");
          return of(null);
        }),
      )
      .subscribe((value) => {
        this.stats = value;
        this.changeDetector.detectChanges();
      });
  }
}
