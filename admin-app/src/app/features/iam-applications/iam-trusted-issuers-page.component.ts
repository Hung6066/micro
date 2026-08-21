import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import {
  HisHopeDataTableColumn,
  HisHopeResourceListPageComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopeI18nService } from "@his-hope/frontend-foundation/i18n";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { IamTrustedIssuersResponse } from "../../core/contracts/admin.contracts";

@Component({
  selector: "app-iam-trusted-issuers-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeResourceListPageComponent],
  template: `
    <hh-resource-list-page
      title="admin.trustedIssuers"
      titleFallback="Trusted issuers"
      subtitle="admin.trustedIssuersSubtitle"
      subtitleFallback="Configured OIDC/SAML issuer metadata."
      [showCreate]="false"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (refresh)="load()"
    />
  `,
})
export class IamTrustedIssuersPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<IamTrustedIssuersResponse>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load trusted issuers.",
  });
  rows: Record<string, unknown>[] = [];
  get loading() {
    return this.state.loading;
  }
  get error() {
    return this.state.error;
  }
  get columns(): HisHopeDataTableColumn[] {
    return [
      { key: "key", label: this.i18n.t("admin.key", "Key") },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "issuer", label: this.i18n.t("admin.issuer", "Issuer") },
      { key: "protocol", label: this.i18n.t("admin.protocol", "Protocol") },
      { key: "active", label: this.i18n.t("admin.active", "Active") },
    ];
  }
  constructor() {
    effect(() => {
      const x = this.state.resource.data();
      if (x) {
        this.rows = x.issuers.map((item: unknown) => ({ ...(item as object) }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit() {
    this.load();
  }
  load() {
    this.state.load(this.api.getIamTrustedIssuers());
  }
}
