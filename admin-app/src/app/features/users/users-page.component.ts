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
import {
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableDetailDirective,
  HisHopeDialogService,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToastService,
  HisHopeToolbarComponent,
  HisHopeActionButtonComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { AdminPageQuery, User } from "../../core/contracts/admin.contracts";
import { UsersApiService } from "../../core/services/users-api.service";
import { HisHopePageQuery } from "@his-hope/frontend-foundation";
import { UserEditDialogComponent } from "./user-edit-dialog.component";
import { AdminResourceTableController } from "../../core/services/admin-resource-table.controller";
import { downloadAdminTableExport } from "../../core/services/admin-query.util";

@Component({
  selector: "app-users-page",
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    HisHopeDataTableCellDirective,
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
        [title]="'admin.pageUsers' | hhTranslate"
        [subtitle]="'admin.usersSubtitle' | hhTranslate"
      />
      <hh-toolbar hhPageToolbar [label]="'admin.users' | hhTranslate">
        <span hhToolbarTitle
          >{{ totalItems }} {{ "admin.users" | hhTranslate }}</span
        >
        <hh-action-button
          *ngIf="canWrite"
          hh-toolbar-actions
          kind="primary"
          icon="person_add"
          [label]="'admin.createUser' | hhTranslate: 'Create user'"
          (pressed)="openCreateDialog()"
        />
        <hh-action-button
          hh-toolbar-actions
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate"
          (pressed)="loadUsers()"
        />
      </hh-toolbar>
      <hh-data-table
        [label]="'admin.users' | hhTranslate"
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
        viewStorageKey="admin.users"
        viewName="default"
        [serverBackedView]="true"
        [savedView]="savedView"
        (rowExpandChange)="toggleRowExpand($event)"
        (viewSaveRequested)="saveView($event)"
        (viewResetRequested)="resetView($event)"
        [emptyMessage]="'admin.noUsers' | hhTranslate"
        (bulkActionRequested)="onBulkAction($event)"
        (exportRequested)="onExport($event)"
        (retry)="loadUsers()"
      >
        <ng-template hhDataTableCell="actions" let-row>
          <hh-action-button
            *ngIf="canWrite"
            (pressed)="openEditDialog(row)"
            kind="row"
            mode="icon-only"
            icon="edit"
            [label]="'admin.edit' | hhTranslate: 'Edit'"
          />
          <hh-action-button
            *ngIf="canWrite"
            (pressed)="toggleUser(row)"
            kind="danger"
            mode="icon-only"
            icon="play_arrow"
            [label]="'admin.toggle' | hhTranslate: 'Toggle status'"
          />
        </ng-template>
        <ng-template hhDataTableDetail let-row>
          <div class="hh-data-table-detail">
            {{ row["email"] }} · {{ row["roles"] }} · {{ row["isActive"] }}
          </div>
        </ng-template>
      </hh-data-table>
    </hh-page-layout>
  `,
})
export class UsersPageComponent implements OnInit {
  private readonly api = inject(UsersApiService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly toast = inject(HisHopeToastService);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  users: User[] = [];
  tableRows: Record<string, unknown>[] = [];
  columns: HisHopeDataTableColumn[] = [];
  bulkActions: HisHopeBulkAction[] = [];

  readonly table = new AdminResourceTableController<AdminPageQuery, User>({
    destroyRef: this.destroyRef,
    i18n: this.i18n,
    initialQuery: { page: 1, pageSize: 20 },
    loader: (query) => this.api.getUsersPage(query),
    loadErrorMessageKey: "admin.loadUsersFailed",
    loadErrorFallback: "Failed to load users.",
    onStateChange: () => this.cdr.markForCheck(),
  });

  get canExport(): boolean {
    return this.permissions.has("reports.export");
  }
  get canWrite(): boolean {
    return this.permissions.has("admin.users.write");
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
        this.users = result.items;
        this.tableRows = result.items.map((user) => ({
          id: user.id,
          userName: user.userName,
          email: user.email,
          roles: (user.roles || []).join(", "),
          isActive: user.isActive ? "Yes" : "No",
          entity: user,
        }));
        this.cdr.markForCheck();
      }
    });
  }

  ngOnInit(): void {
    this.table.loadServerView(() => this.api.getTableViews("users"));
    this.loadUsers();
  }

  loadUsers(query = this.query): void {
    this.table.load(query);
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.loadUsers(query);
  }

  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.table.toggleRowExpand(event);
  }

  saveView(event: { name: string; payload: unknown }): void {
    this.table.saveView(event, (name, payload) =>
      this.api.saveTableView("users", name, payload),
    );
  }

  resetView(event: { name: string }): void {
    this.table.resetView(
      event,
      (name) => this.api.deleteTableView("users", name),
      () => this.table.loadServerView(() => this.api.getTableViews("users")),
    );
  }

  openCreateDialog(): void {
    if (!this.canWrite) return;
    this.dialog
      .open<UserEditDialogComponent, User | null, boolean>(
        UserEditDialogComponent,
        {
          width: "min(840px, calc(100vw - 32px))",
          maxWidth: "calc(100vw - 32px)",
          ariaLabel: this.i18n.t("admin.createUser", "Create user"),
          data: null,
        },
      )
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.loadUsers(this.query);
      });
  }

  openEditDialog(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const user = row["entity"] as User | undefined;
    if (!user?.id) return;
    this.api.getUser(user.id).subscribe({
      next: (detail) =>
        this.dialog
          .open<UserEditDialogComponent, User | null, boolean>(
            UserEditDialogComponent,
            {
              width: "min(840px, calc(100vw - 32px))",
              maxWidth: "calc(100vw - 32px)",
              ariaLabel: this.i18n.t("admin.editUser", "Edit user"),
              data: detail,
            },
          )
          .afterClosed()
          .subscribe((saved) => {
            if (saved) this.loadUsers(this.query);
          }),
      error: () =>
        this.toast.error(
          this.i18n.t("admin.loadUserFailed", "Failed to load user."),
        ),
    });
  }

  toggleUser(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const user = row["entity"] as User | undefined;
    if (!user?.id) return;
    const request = user.isActive
      ? this.api.deactivateUser(user.id)
      : this.api.activateUser(user.id);
    request.subscribe({
      next: () => this.loadUsers(this.query),
      error: () =>
        this.toast.error(
          this.i18n.t("admin.updateUserFailed", "Failed to update user."),
        ),
    });
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.canWrite) return;
    this.table.runBulkAction(
      request,
      (bulkRequest) => this.api.bulkUsers(bulkRequest),
      "admin.updateUsersFailed",
      "Failed to update selected users.",
    );
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable("users", request).subscribe((blob) => {
      downloadAdminTableExport(blob, "users", request.format);
    });
  }

  private createColumns(): HisHopeDataTableColumn[] {
    return [
      {
        key: "id",
        label: this.i18n.t("admin.id"),
        responsivePriority: 3,
        pinned: "left",
      },
      {
        key: "userName",
        label: this.i18n.t("admin.username"),
        sortable: true,
        responsivePriority: 1,
        pinned: "left",
      },
      {
        key: "email",
        label: this.i18n.t("admin.email"),
        sortable: true,
        responsivePriority: 2,
      },
      {
        key: "roles",
        label: this.i18n.t("admin.roles"),
        responsivePriority: 3,
      },
      {
        key: "isActive",
        label: this.i18n.t("admin.active"),
        responsivePriority: 2,
        pinned: "right",
      },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        hideable: false,
        sortable: false,
        reorderable: false,
        width: "var(--size-empty-state-icon)",
        minWidth: 96,
        align: "center" as const,
        responsivePriority: 1,
        pinned: "right",
      },
    ];
  }

  private createBulkActions(): HisHopeBulkAction[] {
    return this.canWrite
      ? [
          {
            id: "activate",
            label: this.i18n.t("admin.activateSelected"),
            icon: "person_add",
          },
          {
            id: "deactivate",
            label: this.i18n.t("admin.deactivateSelected"),
            icon: "person_off",
            tone: "danger",
          },
        ]
      : [];
  }
}
