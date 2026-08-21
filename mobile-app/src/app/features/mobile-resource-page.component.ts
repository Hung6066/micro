import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopePageQuery,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation/contracts";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  HisHopeConfirmDialogComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableDetailDirective,
  HisHopeMobileExportSheetComponent,
  HisHopeMobileResourceListPageComponent,
} from "@his-hope/frontend-foundation/ui";
import { MobileResource } from "../core/contracts/mobile.contracts";
import type { MobileTableResource } from "../core/contracts/mobile.contracts";
import {
  MOBILE_RESOURCE_CONFIGS,
  MobileResourceConfig,
  createMobileResourceServices,
} from "../core/mobile-resource.config";
import { ClientsApiService } from "../core/services/clients-api.service";
import { ConsentsApiService } from "../core/services/consents-api.service";
import { MobileConfirmState } from "../core/services/mobile-confirm-state";
import { downloadMobileTableExport } from "../core/services/mobile-query.util";
import { MobilePagedResourceController } from "../core/services/mobile-paged-resource.controller";
import { MobileTableApiService } from "../core/services/mobile-table-api.service";
import { RolesApiService } from "../core/services/roles-api.service";
import { UsersApiService } from "../core/services/users-api.service";

type MobileRow = Record<string, unknown>;

@Component({
  standalone: true,
  imports: [
    HisHopeConfirmDialogComponent,
    HisHopeDataTableDetailDirective,
    HisHopeMobileExportSheetComponent,
    HisHopeMobileResourceListPageComponent,
    HisHopeTranslatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (controller) {
      <hh-mobile-resource-list-page
        [toolbarLabel]="config.titleKey | hhTranslate: config.titleFallback"
        [countLabel]="config.countLabelKey"
        [countLabelFallback]="config.countLabelFallback"
        [totalItems]="controller.totalCount"
        [canWrite]="canWrite"
        [showCreate]="config.showCreate"
        [createLabel]="'admin.create'"
        [refreshLabel]="'admin.refresh'"
        [searchValue]="controller.query.search ?? ''"
        [searchPlaceholder]="'common.search'"
        [tableLabel]="config.titleKey"
        [tableLabelFallback]="config.titleFallback"
        [actionSheetLabel]="config.titleKey"
        [actionSheetLabelFallback]="config.titleFallback"
        [detailLabel]="'mobile.details'"
        [columns]="columns"
        [rows]="tableRows"
        [loading]="controller.loading || controller.bulkLoading"
        [loadingMore]="controller.loadingMore"
        [error]="controller.displayError"
        [emptyMessage]="config.emptyKey"
        [emptyMessageFallback]="config.emptyFallback"
        [selection]="config.selection"
        [bulkActions]="bulkActions"
        [query]="controller.query"
        [hasMore]="controller.hasMore"
        [nextCursor]="controller.nextCursor ?? ''"
        [detailOpen]="!!selectedRow"
        (create)="onCreate()"
        (refresh)="reload()"
        (searchChange)="search($event)"
        (loadMore)="controller.loadMore($event)"
        (queryChange)="controller.load($event)"
        (rowClick)="selectRow($event)"
        (bulkAction)="onBulkAction($event)"
        (exportRequested)="onExport($event)"
        (detailClose)="selectedRow = null"
      >
        <ng-template hhDataTableDetail let-row
          ><div class="detail">{{ detail(row) }}</div></ng-template
        >
        @if (selectedRow; as row) {
          <dl class="detail-list" hhMobileResourceDetailSheet>
            @for (entry of detailEntries(row); track entry[0]) {
              <div>
                <dt>{{ entry[0] }}</dt>
                <dd>{{ entry[1] }}</dd>
              </div>
            }
            @if (canWrite && config.showCreate) {
              <button
                type="button"
                class="hh-button hh-button--primary detail-edit"
                (click)="editRow(row)"
              >
                {{ "admin.edit" | hhTranslate: "Edit" }}
              </button>
            }
            @if (config.detailPath) {
              <button
                type="button"
                class="hh-button hh-button--secondary detail-edit"
                (click)="openDetail(row)"
              >
                {{ "mobile.details" | hhTranslate: "Details" }}
              </button>
            }
          </dl>
        }
      </hh-mobile-resource-list-page>
    }

    <hh-confirm-dialog
      [open]="confirm.open"
      [title]="confirm.title | hhTranslate"
      [message]="confirm.message | hhTranslate"
      [confirmLabel]="confirm.confirmLabel | hhTranslate"
      (confirmed)="confirm.confirm()"
      (cancelled)="confirm.cancel()"
    />

    <hh-mobile-export-sheet
      [open]="exportOpen"
      (close)="closeExportSheet()"
      (exportRequested)="runExport($event)"
    />
  `,
  styles: [
    `
      :host {
        display: block;
      }
      .detail {
        color: var(--text-secondary);
        line-height: 1.5;
      }
      .detail-list {
        display: grid;
        gap: var(--space-md);
        margin: 0;
      }
      .detail-list div {
        display: grid;
        gap: var(--space-xxs);
      }
      .detail-list dt {
        color: var(--text-muted);
        font-size: var(--font-size-caption);
      }
      .detail-list dd {
        margin: 0;
        color: var(--text-primary);
        overflow-wrap: anywhere;
      }
      .detail-edit {
        margin-top: var(--space-sm);
        width: 100%;
      }
    `,
  ],
})
export class MobileResourcePageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly clientsApi = inject(ClientsApiService);
  private readonly usersApi = inject(UsersApiService);
  private readonly rolesApi = inject(RolesApiService);
  private readonly consentsApi = inject(ConsentsApiService);
  private readonly tableApi = inject(MobileTableApiService);
  private readonly permissions = inject(HisHopePermissionService);
  readonly i18n = inject(HisHopeI18nService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly confirm = new MobileConfirmState();
  exportOpen = false;
  private pendingExportRequest: HisHopeTableExportRequest | null = null;
  selectedRow: MobileRow | null = null;
  resource: MobileResource = "clients";
  config: MobileResourceConfig = MOBILE_RESOURCE_CONFIGS.clients;
  columns: HisHopeDataTableColumn[] = [];
  bulkActions: HisHopeBulkAction[] = [];
  controller: MobilePagedResourceController<HisHopePageQuery, unknown> | null =
    null;

  get tableRows(): MobileRow[] {
    return (
      this.controller?.items.map((item) =>
        this.config.toRow(item, this.i18n),
      ) ?? []
    );
  }

  private readonly services = createMobileResourceServices(
    this.clientsApi,
    this.usersApi,
    this.rolesApi,
    this.consentsApi,
  );

  get canWrite(): boolean {
    return this.permissions.has(this.config.writePermission);
  }

  constructor() {
    effect(() => {
      this.i18n.locale();
      this.columns = this.config.createColumns(this.i18n);
      this.bulkActions = this.config.createBulkActions(this.i18n, this.canWrite);
      this.cdr.markForCheck();
    });
  }

  ngOnInit(): void {
    this.route.data.subscribe((data) => {
      this.resource = data["resource"] as MobileResource;
      this.config = MOBILE_RESOURCE_CONFIGS[this.resource];
      this.selectedRow = null;
      this.controller = new MobilePagedResourceController<
        HisHopePageQuery,
        unknown
      >({
        i18n: this.i18n,
        initialQuery: { page: 1, pageSize: 20 },
        loader: (query) => this.config.loader(this.services, query),
        loadErrorMessageKey: this.config.loadErrorKey,
        loadErrorFallback: this.config.loadErrorFallback,
        loadMoreErrorMessageKey: this.config.loadMoreErrorKey,
        loadMoreErrorFallback: this.config.loadMoreErrorFallback,
        onStateChange: () => this.cdr.markForCheck(),
      });
      this.reload();
    });
  }

  reload(): void {
    this.controller?.load({
      page: 1,
      pageSize: 20,
      search: this.controller.query.search,
    });
  }

  search(value: string): void {
    if (!this.controller) return;
    this.controller.load({
      ...this.controller.query,
      page: 1,
      cursor: undefined,
      search: value || undefined,
    });
  }

  selectRow(row: MobileRow): void {
    this.selectedRow = row;
    this.cdr.markForCheck();
  }

  detailEntries(row: MobileRow): Array<[string, string]> {
    return this.columns
      .map((column) => {
        const value = row[column.key];
        if (value === undefined || value === null || value === "") {
          return null;
        }
        return [column.label, String(value)] as [string, string];
      })
      .filter((entry): entry is [string, string] => entry !== null);
  }

  detail(row: MobileRow): string {
    return this.detailEntries(row)
      .map(([label, value]) => `${label}: ${value}`)
      .join(" · ");
  }

  onCreate(): void {
    void this.router.navigateByUrl(this.editBasePath() + "/new");
  }

  editRow(row: MobileRow): void {
    const id = String(row["id"] ?? "");
    if (!id) return;
    this.selectedRow = null;
    void this.router.navigateByUrl(`${this.editBasePath()}/${encodeURIComponent(id)}/edit`);
  }

  openDetail(row: MobileRow): void {
    const id = String(row["id"] ?? "");
    const detailPath = this.config.detailPath;
    if (!id || !detailPath) return;
    this.selectedRow = null;
    void this.router.navigateByUrl(detailPath(id));
  }

  private editBasePath(): string {
    return `/admin/${this.resource}`;
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.controller) return;
    const bulk = this.config.bulk;
    if (!bulk) return;
    const run = () =>
      this.controller!.runBulkAction(
        request,
        (payload) => bulk(this.services, payload),
        "mobile.bulkFailed",
        "Bulk action failed.",
      );
    if (request.actionId === "delete" || request.actionId === "deactivate") {
      this.confirm.ask("common.confirmContinue", run, {
        title: "common.confirmAction",
        confirmLabel: "common.yes",
      });
      this.cdr.markForCheck();
      return;
    }
    run();
  }

  onExport(request: HisHopeTableExportRequest): void {
    if (this.resource === "consents") {
      this.controller?.setActionError(
        this.i18n.t(
          "mobile.exportDesktop",
          "Export is available from the desktop admin workspace.",
        ),
      );
      return;
    }
    this.pendingExportRequest = request;
    this.exportOpen = true;
    this.cdr.markForCheck();
  }

  closeExportSheet(): void {
    this.exportOpen = false;
    this.pendingExportRequest = null;
    this.cdr.markForCheck();
  }

  runExport(format: "csv" | "json"): void {
    const pending = this.pendingExportRequest;
    if (!pending || this.resource === "consents") return;
    this.exportOpen = false;
    this.tableApi
      .export(this.resource as MobileTableResource, { ...pending, format })
      .subscribe({
        next: (blob) => {
          downloadMobileTableExport(blob, this.resource, format);
          this.pendingExportRequest = null;
          this.cdr.markForCheck();
        },
        error: () => {
          this.controller?.setActionError(
            this.i18n.t("mobile.exportFailed", "Export failed."),
          );
          this.pendingExportRequest = null;
          this.cdr.markForCheck();
        },
      });
  }
}
