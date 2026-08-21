import {
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HisHopeDialogService } from "@his-hope/frontend-foundation/ui";
import {
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
  HisHopePageQuery,
} from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableDetailDirective,
  HisHopeActionButtonComponent,
  HisHopeConfirmDialogComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { AdminPageQuery, Role } from "../../core/contracts/admin.contracts";
import { AdminTableApiService } from "../../core/services/admin-table-api.service";
import { RolesApiService } from "../../core/services/roles-api.service";
import { RoleEditDialogComponent } from "./role-edit-dialog.component";
import { AdminResourceTableController } from "../../core/services/admin-resource-table.controller";
import { AdminConfirmState } from "../../core/services/admin-confirm-state";
import { downloadAdminTableExport } from "../../core/services/admin-query.util";

@Component({
  selector: "app-roles-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    HisHopeDataTableComponent,
    HisHopeDataTableDetailDirective,
    HisHopeActionButtonComponent,
    HisHopeConfirmDialogComponent,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
    HisHopeTranslatePipe,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.pageRoles' | hhTranslate"
        [subtitle]="'admin.rolesSubtitle' | hhTranslate"
      />
      <hh-toolbar hhPageToolbar [label]="'admin.roles' | hhTranslate">
        <span hhToolbarTitle
          >{{ totalItems }} {{ "admin.roles" | hhTranslate }}</span
        >
        <hh-action-button
          *ngIf="canWrite"
          hh-toolbar-actions
          kind="primary"
          icon="add"
          [label]="'admin.createRole' | hhTranslate: 'Create role'"
          (pressed)="openCreateRole()"
        />
        <hh-action-button
          hh-toolbar-actions
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate"
          (pressed)="loadRoles()"
        />
      </hh-toolbar>
      <hh-data-table
        [label]="'admin.roles' | hhTranslate"
        [columns]="columns"
        [rows]="tableRows"
        [selection]="true"
        [loading]="loading"
        mode="server"
        [totalItems]="totalItems"
        [query]="query"
        [pageSize]="20"
        (queryChange)="onQueryChange($event)"
        [error]="error ?? ''"
        [empty]="!loading && !error && tableRows.length === 0"
        [exportable]="canExport"
        [bulkActions]="bulkActions"
        [filterBuilder]="true"
        [virtualizeColumns]="true"
        [expandedRowKeys]="expandedRowKeys"
        viewStorageKey="admin.roles"
        viewName="default"
        [serverBackedView]="true"
        [savedView]="savedView"
        (rowExpandChange)="toggleRowExpand($event)"
        (viewSaveRequested)="saveView($event)"
        (viewResetRequested)="resetView($event)"
        (bulkActionRequested)="onBulkAction($event)"
        (exportRequested)="onExport($event)"
        [emptyMessage]="'admin.noRoles' | hhTranslate"
        (retry)="loadRoles()"
      >
        <ng-template hhDataTableDetail let-row>
          <div class="hh-data-table-detail">
            <p>
              {{ row["description"] || ("admin.noDescription" | hhTranslate) }}
            </p>
            <hh-action-button
              *ngIf="canWrite"
              (pressed)="openEditRole(row)"
              kind="secondary"
              icon="edit"
              [label]="'admin.editRole' | hhTranslate: 'Edit role'"
            />
            <hh-action-button
              *ngIf="canWrite"
              (pressed)="publishRole(row)"
              kind="secondary"
              icon="publish"
              [label]="'admin.publishRole' | hhTranslate"
            />
            <hh-action-button
              *ngIf="canWrite"
              (pressed)="rollbackRole(row)"
              kind="secondary"
              icon="undo"
              [label]="'admin.rollbackRole' | hhTranslate"
            />
          </div>
        </ng-template>
      </hh-data-table>
      <hh-confirm-dialog
        [open]="confirm.open"
        [title]="confirm.title"
        [message]="confirm.message"
        [confirmLabel]="confirm.confirmLabel"
        (confirmed)="confirm.confirm()"
        (cancelled)="confirm.cancel()"
      />
    </hh-page-layout>
  `,
})
export class RolesPageComponent implements OnInit {
  private readonly api = inject(RolesApiService);
  private readonly tableApi = inject(AdminTableApiService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  roles: Role[] = [];
  tableRows: Record<string, unknown>[] = [];
  columns: HisHopeDataTableColumn[] = [];
  bulkActions: HisHopeBulkAction[] = [];
  readonly confirm = new AdminConfirmState();

  readonly table = new AdminResourceTableController<AdminPageQuery, Role>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    initialQuery: { page: 1, pageSize: 20 },
    loader: (query) => this.api.getRolesPage(query),
    loadErrorMessageKey: "admin.loadRolesFailed",
    loadErrorFallback: "Failed to load roles.",
    onStateChange: () => this.cdr.markForCheck(),
  });

  get canExport(): boolean {
    return this.permissions.has("reports.export");
  }
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  get loading(): boolean {
    return this.table.loading;
  }
  get error(): string | null {
    return this.table.error;
  }
  get totalItems(): number {
    return this.table.totalItems;
  }
  get query(): AdminPageQuery {
    return this.table.query;
  }
  get savedView() {
    return this.table.savedView;
  }
  get expandedRowKeys() {
    return this.table.expandedRowKeys;
  }

  constructor() {
    effect(() => {
      this.i18n.locale();
      this.columns = this.createColumns();
      this.bulkActions = this.createBulkActions();
      this.cdr.markForCheck();
    });
    effect(() => {
      const result = this.table.resource.data();
      if (result) {
        this.roles = result.items;
        this.tableRows = result.items.map((role) => ({
          ...role,
          owner: role.owner || "identity-service",
          riskTier: role.riskTier || "standard",
          version: role.version || 1,
        }));
        this.cdr.markForCheck();
      }
    });
  }

  ngOnInit(): void {
    this.table.loadServerView(() => this.tableApi.getViews("roles"));
    this.loadRoles();
  }

  openCreateRole(): void {
    if (!this.canWrite) return;
    this.dialog
      .open(RoleEditDialogComponent, { width: "560px", data: null })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.loadRoles(this.query);
      });
  }

  openEditRole(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const role = this.roles.find((item) => item.id === String(row["id"] ?? ""));
    if (!role) return;
    this.dialog
      .open(RoleEditDialogComponent, { width: "560px", data: role })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.loadRoles(this.query);
      });
  }

  saveView(event: { name: string; payload: unknown }): void {
    this.table.saveView(event, (name, payload) =>
      this.tableApi.saveView("roles", name, payload),
    );
  }

  resetView(event: { name: string }): void {
    this.table.resetView(
      event,
      (name) => this.tableApi.deleteView("roles", name),
      () => this.table.loadServerView(() => this.tableApi.getViews("roles")),
    );
  }

  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.table.toggleRowExpand(event);
  }

  loadRoles(query = this.query): void {
    this.table.load(query);
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.loadRoles(query);
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.canWrite) return;
    this.table.runBulkAction(
      request,
      (bulkRequest) => this.tableApi.bulk("roles", bulkRequest),
      "admin.updateRolesFailed",
      "Failed to update selected roles.",
    );
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.tableApi.export("roles", request).subscribe((blob) => {
      downloadAdminTableExport(blob, "roles", request.format);
    });
  }

  publishRole(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id) return;
    this.confirm.ask("admin.confirmRolePublish", () => this.runPublish(id), {
      confirmLabel: "admin.publishRole",
    });
  }

  rollbackRole(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id) return;
    this.confirm.ask(
      "admin.confirmRoleRollback",
      () => this.runRollback(id),
      { confirmLabel: "admin.rollbackRole" },
    );
  }

  private runPublish(id: string): void {
    this.api.publishRole(id).subscribe({
      next: () => this.loadRoles(this.query),
      error: () =>
        this.table.setActionError(this.i18n.t("admin.rolePublishFailed")),
    });
  }

  private runRollback(id: string): void {
    this.api.rollbackRole(id).subscribe({
      next: () => this.loadRoles(this.query),
      error: () =>
        this.table.setActionError(this.i18n.t("admin.roleRollbackFailed")),
    });
  }

  private createColumns(): HisHopeDataTableColumn[] {
    return [
      {
        key: "name",
        label: this.i18n.t("admin.name"),
        sortable: true,
        responsivePriority: 1,
        pinned: "left",
      },
      {
        key: "description",
        label: this.i18n.t("admin.description"),
        responsivePriority: 2,
      },
      {
        key: "owner",
        label: this.i18n.t("admin.permissionOwner", "Owner"),
        responsivePriority: 3,
      },
      { key: "riskTier", label: this.i18n.t("admin.permissionRisk", "Risk") },
      {
        key: "version",
        label: this.i18n.t("admin.permissionVersion", "Version"),
      },
    ];
  }

  private createBulkActions(): HisHopeBulkAction[] {
    return this.canWrite
      ? [
          {
            id: "delete",
            label: this.i18n.t("admin.deleteSelected"),
            tone: "danger",
          },
        ]
      : [];
  }
}
