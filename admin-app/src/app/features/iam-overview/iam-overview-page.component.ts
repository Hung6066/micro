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
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { IamOverview } from "../../core/contracts/admin.contracts";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-iam-overview-page",
  standalone: true,
  imports: [
    HisHopeActionButtonComponent,
    CommonModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<hh-page-layout
    ><hh-page-header
      hhPageHeader
      [title]="'admin.iamOverview' | hhTranslate: 'IAM overview'"
      [subtitle]="
        'admin.iamOverviewSubtitle'
          | hhTranslate: 'Identity, authorization and governance posture.'
      "
    /><hh-toolbar hhPageToolbar
      ><hh-action-button
        [disabled]="state.loading"
        (pressed)="load()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
    /></hh-toolbar>
    @if (state.loading) {
      <hh-state kind="loading" message="state.loading" />
    } @else if (state.error) {
      <hh-state kind="error" message="admin.iamLoadFailed" />
    } @else if (state.resource.data()) {
      <section class="iam-overview__grid">
        <article *ngFor="let card of cards" class="iam-overview__card">
          <span>{{ card.label }}</span
          ><strong>{{ card.value }}</strong>
        </article>
      </section>
    }
  </hh-page-layout>`,
  styles: [
    `
      .iam-overview__grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: var(--space-3);
        min-width: 0;
      }
      .iam-overview__card {
        display: flex;
        flex-direction: column;
        gap: var(--space-2);
        min-width: 0;
        padding: var(--space-4);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-card);
        background: var(--surface-muted);
        color: var(--text-secondary);
      }
      .iam-overview__card span {
        display: block;
        line-height: 1.4;
      }
      .iam-overview__card strong {
        display: block;
        color: var(--text-primary);
        font-size: 2rem;
        line-height: 1;
        font-weight: var(--font-weight-semibold);
      }
      .iam-overview__card--attention {
        border-color: var(--color-warning);
      }
      @media (max-width: 720px) {
        .iam-overview__grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
      @media (max-width: 420px) {
        .iam-overview__grid {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class IamOverviewPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<IamOverview>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load IAM overview.",
  });
  cards: Array<{ label: string; value: number }> = [];
  constructor() {
    effect(() => {
      const overview = this.state.resource.data();
      if (overview) {
        this.cards = [
          ["admin.scopes", overview.scopes],
          ["admin.services", overview.services],
          ["admin.publishedPermissionSets", overview.publishedPermissionSets],
          ["admin.activeAssignments", overview.activeAssignments],
          ["admin.groups", overview.groups],
          ["admin.workloadRoles", overview.workloadRoles],
          ["admin.pendingAccessRequests", overview.pendingAccessRequests],
          ["admin.pendingAccessReviews", overview.pendingAccessReviews],
          ["admin.pendingBreakGlass", overview.pendingBreakGlass],
          ["admin.auditEventsLast24Hours", overview.auditEventsLast24Hours],
        ].map(([key, value]) => ({
          label: this.i18n.t(String(key), String(key)),
          value: Number(value),
        }));
      }
    });
  }
  ngOnInit() {
    this.load();
  }
  load() {
    this.state.load(this.api.getIamOverview());
  }
}
