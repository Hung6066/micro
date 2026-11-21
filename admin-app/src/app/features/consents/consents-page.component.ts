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
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import { HisHopePageQuery } from "@his-hope/frontend-foundation/contracts";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { AdminPageQuery, Consent } from "../../core/contracts/admin.contracts";
import { ConsentsApiService } from "../../core/services/consents-api.service";
import { AdminResourceTableController } from "../../core/services/admin-resource-table.controller";
import { TenantContextService } from "../../core/services/tenant-context.service";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-consents-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    HisHopeActionButtonComponent,
    HisHopeDataTableComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `<hh-page-layout>
    <hh-page-header
      hhPageHeader
      [title]="'admin.pageConsents' | hhTranslate"
      [subtitle]="'admin.consentsSubtitle' | hhTranslate"
    />
    <hh-toolbar hhPageToolbar [label]="'admin.consentControls' | hhTranslate">
      <span hhToolbarTitle
        >{{ totalItems }} {{ "admin.consents" | hhTranslate }}</span
      >
      <hh-action-button
        (pressed)="loadConsents()"
        hh-toolbar-actions
        kind="secondary"
        icon="refresh"
        [label]="'admin.refresh' | hhTranslate"
      />
    </hh-toolbar>
    <hh-data-table
      [label]="'admin.pageConsents' | hhTranslate"
      [columns]="columns"
      [rows]="tableRows"
      [selection]="true"
      [loading]="loading"
      mode="server"
      [totalItems]="totalItems"
      [query]="query"
      [pageSize]="20"
      (queryChange)="onQueryChange($event)"
      [error]="error"
      [empty]="!loading && !error && tableRows.length === 0"
      [emptyMessage]="'admin.noConsents' | hhTranslate"
      (retry)="loadConsents()"
    />
  </hh-page-layout>`,
})
export class ConsentsPageComponent implements OnInit {
  private readonly api = inject(ConsentsApiService);
  private readonly tenantContext = inject(TenantContextService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);
  consents: Consent[] = [];
  get columns(): HisHopeDataTableColumn[] {
    return [
      {
        key: "subject",
        label: this.i18n.t("admin.subject", "Subject"),
        sortable: true,
        responsivePriority: 1,
      },
      {
        key: "clientId",
        label: this.i18n.t("admin.clientId", "Client ID"),
        responsivePriority: 2,
      },
      {
        key: "scopes",
        label: this.i18n.t("admin.scopes", "Scopes"),
        responsivePriority: 3,
      },
      {
        key: "created",
        label: this.i18n.t("admin.created", "Created"),
        sortable: true,
        responsivePriority: 2,
        format: "dateTime",
      },
    ];
  }
  tableRows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly table = new AdminResourceTableController<AdminPageQuery, Consent>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    initialQuery: { page: 1, pageSize: 20 },
    loader: (query) => this.api.getConsentsPage(query),
    loadErrorMessageKey: "admin.loadConsentsFailed",
    loadErrorFallback: "Failed to load consents.",
    onStateChange: () => this.cdr.markForCheck(),
  });
  get loading(): boolean {
    return this.table.loading;
  }
  get error(): string {
    return this.table.error ?? "";
  }
  get totalItems(): number {
    return this.table.totalItems;
  }
  get query(): AdminPageQuery {
    return this.table.query;
  }
  constructor() {
    effect(() => {
      const result = this.table.resource.data();
      if (result) {
        this.consents = result.items;
        this.tableRows = result.items.map((consent: Consent) => ({
          id: consent.id,
          subject: consent.subject,
          clientId: consent.clientId,
          scopes: (consent.scopes || []).join(", "),
          created: consent.created,
        }));
        this.cdr.markForCheck();
      }
    });
  }
  ngOnInit(): void {
    this.loadConsents();
    this.tenantContext.bindTenantReload(this.destroyRef, () => this.loadConsents());
  }
  loadConsents(query = this.query): void {
    this.table.load(query);
  }
  onQueryChange(query: HisHopePageQuery): void {
    this.loadConsents(query);
  }
}
