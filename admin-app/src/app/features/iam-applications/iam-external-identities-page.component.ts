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
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { IamApiService } from "../../core/services/iam-api.service";
import { AdminResourceStateController } from "../../core/services/admin-resource-state.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";

@Component({
  selector: "app-iam-external-identities-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeResourceListPageComponent],
  template: `
    <hh-resource-list-page
      title="admin.externalIdentities"
      titleFallback="External identities"
      subtitle="admin.externalIdentitiesSubtitle"
      subtitleFallback="Configured browser federation providers. Secrets stay server-side."
      [showCreate]="false"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (refresh)="load()"
    />
  `,
})
export class IamExternalIdentitiesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    providers: Array<{
      provider: string;
      displayName: string;
      icon?: string;
      protocol?: string;
      loginUrl?: string;
    }>;
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load external identities.",
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
      { key: "provider", label: this.i18n.t("admin.provider", "Provider") },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
      },
      { key: "protocol", label: this.i18n.t("admin.protocol", "Protocol") },
      { key: "loginUrl", label: this.i18n.t("admin.loginUrl", "Login URL") },
    ];
  }
  constructor() {
    effect(() => {
      const x = this.state.resource.data();
      if (x) {
        this.rows = x.providers.map((item: unknown) => ({
          ...(item as object),
        }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit() {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load() {
    this.state.load(this.api.getExternalIdentityProviders());
  }
}
