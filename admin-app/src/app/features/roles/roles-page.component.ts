import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import {
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
  HisHopePageQuery,
} from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceState } from "@his-hope/frontend-foundation/query";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableDetailDirective,
  HisHopeActionButtonComponent,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import {
  AdminPageQuery,
  AdminPageResult,
  AdminSavedTableView,
  Role,
} from "../../core/contracts/admin.contracts";
import { AdminTableApiService } from "../../core/services/admin-table-api.service";
import { RolesApiService } from "../../core/services/roles-api.service";
import { catchError, finalize } from "rxjs/operators";
import { of } from "rxjs";
import { RoleEditDialogComponent } from "./role-edit-dialog.component";

@Component({
  selector: "app-roles-page",
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    HisHopeDataTableComponent,
    HisHopeDataTableDetailDirective,
    HisHopeActionButtonComponent,
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
    </hh-page-layout>
  `,
})
export class RolesPageComponent implements OnInit {
  private readonly api = inject(RolesApiService);
  private readonly tableApi = inject(AdminTableApiService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canExport(): boolean {
    return this.permissions.has("reports.export");
  }
  get canWrite(): boolean {
    return this.permissions.has("admin.roles.write");
  }
  roles: Role[] = [];
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
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
  tableRows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<AdminPageResult<Role>>(
    this.destroyRef,
  );
  bulkLoading = false;
  private actionError: string | null = null;
  get loading(): boolean {
    return this.resource.loading() || this.bulkLoading;
  }
  get error(): string | null {
    return (
      this.actionError ||
      (this.resource.error() ? this.resource.errorMessage() : null)
    );
  }
  totalItems = 0;
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  savedView: AdminSavedTableView = {};
  expandedRowKeys: string[] = [];
  constructor() {
    effect(() => {
      const result = this.resource.data();
      if (result) {
        this.totalItems = result.totalCount;
        this.roles = result.items;
        this.tableRows = result.items.map((role) => ({
          id: role.id,
          name: role.name,
          description: role.description,
          owner: role.owner || "identity-service",
          riskTier: role.riskTier || "standard",
          version: role.version || 1,
        }));
        this.cdr.markForCheck();
      }
    });
  }
  get bulkActions(): HisHopeBulkAction[] {
    this.i18n.locale();
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

  ngOnInit(): void {
    this.loadServerView();
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

  loadServerView(): void {
    this.tableApi.getViews("roles").subscribe((views) => {
      const view = views.find((item) => item.name === "default") ?? views[0];
      if (view) this.savedView = JSON.parse(view.payloadJson);
    });
  }
  saveView(event: { name: string; payload: unknown }): void {
    this.tableApi.saveView("roles", event.name, event.payload).subscribe();
  }
  resetView(event: { name: string }): void {
    this.savedView = {};
    this.tableApi
      .deleteView("roles", event.name)
      .subscribe({ error: () => this.loadServerView() });
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
  }

  loadRoles(query = this.query): void {
    this.query = query;
    this.actionError = null;
    this.resource.load(
      this.api.getRolesPage(query).pipe(
        catchError(() => {
          this.actionError = this.i18n.t(
            "admin.loadRolesFailed",
            "Failed to load roles.",
          );
          return of({
            items: [],
            totalCount: 0,
            page: query.page,
            pageSize: query.pageSize,
            totalPages: 0,
            hasNextPage: false,
            hasPreviousPage: false,
          });
        }),
      ),
    );
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.loadRoles(query);
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.canWrite) return;
    this.bulkLoading = true;
    this.tableApi
      .bulk("roles", request)
      .pipe(
        finalize(() => (this.bulkLoading = false)),
        catchError(() => {
          this.actionError = this.i18n.t(
            "admin.updateRolesFailed",
            "Failed to update selected roles.",
          );
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) this.loadRoles(this.query);
      });
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.tableApi.export("roles", request).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `roles-${new Date().toISOString().slice(0, 10)}.${request.format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  publishRole(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id || !window.confirm(this.i18n.t("admin.confirmRolePublish"))) return;
    this.api.publishRole(id).subscribe({
      next: () => this.loadRoles(this.query),
      error: () => (this.actionError = this.i18n.t("admin.rolePublishFailed")),
    });
  }

  rollbackRole(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const id = String(row["id"] ?? "");
    if (!id || !window.confirm(this.i18n.t("admin.confirmRoleRollback")))
      return;
    this.api.rollbackRole(id).subscribe({
      next: () => this.loadRoles(this.query),
      error: () => (this.actionError = this.i18n.t("admin.roleRollbackFailed")),
    });
  }
}
