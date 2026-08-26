import { DatePipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  inject,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatSelectModule } from "@angular/material/select";
import {
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeStateComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeApiErrorMessageService as ApiErrorMessageService,
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { HisHopeCommerceRfqDto } from "@his-hope/frontend-foundation/contracts";
import { CommerceApiService } from "../../core/services/commerce-api.service";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatSelectModule,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeStateComponent,
    HisHopeTranslatePipe,
  ],
  templateUrl: "./rfqs-page.component.html",
  styleUrls: ["./rfqs-page.component.scss"],
})
export class RfqsPageComponent implements OnInit {
  private readonly api = inject(CommerceApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly errors = inject(ApiErrorMessageService);
  readonly i18n = inject(HisHopeI18nService);

  readonly loading = signal(true);
  readonly error = signal("");
  readonly rfqs = signal<HisHopeCommerceRfqDto[]>([]);
  tenantLabel: string | null = null;
  readonly drafts = signal<Record<string, { quotedTotal: number; operatorNotes: string; status: string }>>({});

  get pageSubtitle(): string {
    this.i18n.locale();
    return this.i18n.t("operator.rfq.tenantScope", "Tenant: {{tenant}}", {
      tenant: this.tenantLabel ?? this.i18n.t("customerPortal.tenantUnknown", "—"),
    });
  }

  ngOnInit(): void {
    this.tenantContext.activeTenantLabel$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((label) => {
        this.tenantLabel = label;
        this.cdr.markForCheck();
      });

    this.tenantContext.activeTenantKey$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadRfqs());
  }

  draftFor(rfq: HisHopeCommerceRfqDto) {
    const existing = this.drafts()[rfq.id];
    if (existing) return existing;
    return {
      quotedTotal: rfq.quotedTotal ?? 0,
      operatorNotes: rfq.operatorNotes ?? "",
      status: rfq.status === "pending" ? "quoted" : rfq.status,
    };
  }

  updateDraft(
    rfqId: string,
    patch: Partial<{ quotedTotal: number; operatorNotes: string; status: string }>,
  ): void {
    const rfq = this.rfqs().find((item) => item.id === rfqId);
    if (!rfq) return;
    this.drafts.set({
      ...this.drafts(),
      [rfqId]: { ...this.draftFor(rfq), ...patch },
    });
  }

  respond(rfq: HisHopeCommerceRfqDto): void {
    const draft = this.draftFor(rfq);
    this.api
      .respondToRfq(rfq.id, draft)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.rfqs.set(this.rfqs().map((item) => (item.id === updated.id ? updated : item)));
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.rfq.error"));
          this.cdr.markForCheck();
        },
      });
  }

  private loadRfqs(): void {
    this.loading.set(true);
    this.error.set("");
    this.api
      .getRfqs()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.rfqs.set(response.items ?? []);
          this.loading.set(false);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.error.set(this.errors.message(err, "operator.rfq.error"));
          this.loading.set(false);
          this.cdr.markForCheck();
        },
      });
  }
}
