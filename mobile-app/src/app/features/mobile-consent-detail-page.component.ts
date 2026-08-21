import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  inject,
} from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { formatHisHopeDateTime } from "@his-hope/mobile-foundation";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeDescriptionItem,
  HisHopeDescriptionListComponent,
  HisHopeMobileIconComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import { catchError, of } from "rxjs";
import { Consent } from "../core/contracts/mobile.contracts";
import { ConsentsApiService } from "../core/services/consents-api.service";

@Component({
  standalone: true,
  imports: [
    HisHopeDescriptionListComponent,
    HisHopeMobileIconComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="consent-detail">
      <header class="consent-detail__header">
        <button
          type="button"
          class="consent-detail__back"
          [attr.aria-label]="'common.back' | hhTranslate: 'Go back'"
          (click)="goBack()"
        >
          <hh-mobile-icon name="next" />
        </button>
        <div>
          <h1>{{ "admin.consents" | hhTranslate: "Consents" }}</h1>
          <p>
            {{
              "mobile.consentDetailSubtitle"
                | hhTranslate: "Review the scopes this client holds."
            }}
          </p>
        </div>
      </header>

      @if (loading) {
        <hh-state kind="loading" message="admin.loading" />
      } @else if (error) {
        <hh-state kind="error" [message]="error" />
      } @else if (!consent) {
        <hh-state kind="empty" message="state.notFound" />
      } @else {
        <hh-description-list [items]="details" />
        <p class="consent-detail__note" role="note">
          <hh-mobile-icon name="security" size="small" />
          {{
            "mobile.consentRevokeDesktop"
              | hhTranslate
                : "Revoking a consent is available from the desktop admin workspace."
          }}
        </p>
      }
    </section>
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .consent-detail {
        display: grid;
        gap: var(--space-lg);
      }
      .consent-detail__header {
        display: flex;
        align-items: flex-start;
        gap: var(--space-inset);
      }
      .consent-detail__back {
        display: grid;
        place-items: center;
        flex: 0 0 44px;
        width: var(--touch-target);
        height: var(--touch-target);
        padding: 0;
        border: 0;
        border-radius: var(--radius-control);
        background: var(--surface-white);
        color: var(--text-primary);
        transform: rotate(180deg);
      }
      .consent-detail__header h1 {
        margin: 0;
        font-size: var(--font-size-section);
      }
      .consent-detail__header p {
        margin: var(--space-2xs) 0 0;
        color: var(--text-secondary);
        font-size: var(--font-size-label);
      }
      .consent-detail__note {
        display: flex;
        align-items: flex-start;
        gap: var(--space-sm);
        margin: 0;
        padding: var(--space-md);
        border: 1px solid var(--border-default);
        border-radius: var(--radius-control);
        background: var(--surface-subtle);
        color: var(--text-secondary);
        font-size: var(--font-size-label);
        line-height: 1.5;
      }
    `,
  ],
})
export class MobileConsentDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ConsentsApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  error = "";
  consent: Consent | null = null;
  details: HisHopeDescriptionItem[] = [];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get("id");
    if (!id) {
      this.loading = false;
      return;
    }
    this.api
      .getConsent(id)
      .pipe(
        catchError(() => {
          this.error = this.i18n.t(
            "admin.loadConsentsFailed",
            "Failed to load consents.",
          );
          return of(null);
        }),
      )
      .subscribe((consent) => {
        this.consent = consent;
        this.details = consent ? this.toDetails(consent) : [];
        this.loading = false;
        this.cdr.markForCheck();
      });
  }

  goBack(): void {
    void this.router.navigateByUrl("/admin/consents");
  }

  private toDetails(consent: Consent): HisHopeDescriptionItem[] {
    return [
      { term: this.i18n.t("admin.subject", "Subject"), description: consent.subject },
      {
        term: this.i18n.t("admin.clientId", "Client ID"),
        description: consent.clientId,
      },
      {
        term: this.i18n.t("admin.scopes", "Scopes"),
        description: (consent.scopes || []).join(", "),
      },
      {
        term: this.i18n.t("admin.created", "Created"),
        description: formatHisHopeDateTime(consent.created),
      },
    ];
  }
}
