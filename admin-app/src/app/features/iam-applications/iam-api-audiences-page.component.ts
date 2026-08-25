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
import {
  IamApiAudiencesResponse,
  IamScope,
} from "../../core/contracts/admin.contracts";
import { forkJoin, map } from "rxjs";

@Component({
  selector: "app-iam-api-audiences-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HisHopeResourceListPageComponent],
  template: `
    <hh-resource-list-page
      title="admin.apiAudiences"
      titleFallback="API audiences"
      subtitle="admin.apiAudiencesSubtitle"
      subtitleFallback="Resource audiences exposed by Identity Service."
      [showCreate]="false"
      [columns]="columns"
      [rows]="rows"
      [loading]="loading"
      [error]="error"
      (refresh)="load()"
    />
  `,
})
export class IamApiAudiencesPageComponent implements OnInit {
  private readonly api = inject(IamApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  readonly state = new AdminResourceStateController<{
    audiences: IamApiAudiencesResponse["audiences"];
    scopes: IamScope[];
  }>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    loadErrorMessageKey: "admin.iamLoadFailed",
    loadErrorFallback: "Unable to load API audiences.",
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
      { key: "audience", label: this.i18n.t("admin.audience", "Audience") },
      {
        key: "scopeId",
        label: this.i18n.t("admin.scopeId", "Scope"),
        format: {
          type: "friendlyReference",
          references: this.state.resource.data()?.scopes ?? [],
        },
      },
      { key: "lifecycleStatus", label: this.i18n.t("admin.status", "Status") },
    ];
  }
  constructor() {
    effect(() => {
      const x = this.state.resource.data();
      if (x) {
        this.rows = x.audiences.map((item) => ({ ...item }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit() {
    this.load();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.load());
  }
  load() {
    this.state.load(
      forkJoin({
        audiences: this.api
          .getIamApiAudiences()
          .pipe(map((response) => response.audiences)),
        scopes: this.api
          .getIamScopes()
          .pipe(map((scopes) => this.tenantContext.filterScopes(scopes))),
      }),
    );
  }
}
