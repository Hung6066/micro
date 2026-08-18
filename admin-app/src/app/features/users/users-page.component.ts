import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatButtonModule } from "@angular/material/button";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatIconModule } from "@angular/material/icon";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import {
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { HisHopeResourceStore } from "@his-hope/frontend-foundation/query";
import {
  HisHopeDataTableCellDirective,
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
  HisHopeDataTableDetailDirective,
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
  User,
} from "../../core/contracts/admin.contracts";
import { UsersApiService } from "../../core/services/users-api.service";
import { HisHopePageQuery } from "@his-hope/frontend-foundation";
import { catchError, finalize } from "rxjs/operators";
import { of } from "rxjs";
import { UserEditDialogComponent } from "./user-edit-dialog.component";

@Component({
  selector: "app-users-page",
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatSnackBarModule,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeDataTableDetailDirective,
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
          >{{ tableRows.length }} {{ "admin.users" | hhTranslate }}</span
        >
        <button
          *ngIf="canWrite"
          hh-toolbar-actions
          type="button"
          class="hh-button hh-button--primary"
          (click)="openCreateDialog()"
        >
          <span class="material-icons" aria-hidden="true">person_add</span>
          {{ "admin.createUser" | hhTranslate: "Create user" }}
        </button>
        <button
          hh-toolbar-actions
          type="button"
          class="hh-icon-button"
          (click)="loadUsers()"
          [attr.aria-label]="'admin.refresh' | hhTranslate"
          [attr.title]="'admin.refresh' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">refresh</span>
        </button>
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
          <button
            *ngIf="canWrite"
            mat-icon-button
            type="button"
            (click)="openEditDialog(row)"
            [attr.aria-label]="'admin.edit' | hhTranslate"
          >
            <mat-icon>edit</mat-icon>
          </button>
          <button
            *ngIf="canWrite"
            mat-icon-button
            type="button"
            (click)="toggleUser(row)"
            [attr.aria-label]="
              (row['isActive'] === 'Yes'
                ? 'admin.deactivate'
                : 'admin.activate'
              ) | hhTranslate
            "
          >
            <mat-icon>{{
              row["isActive"] === "Yes" ? "person_off" : "person_add"
            }}</mat-icon>
          </button>
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
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canExport(): boolean {
    return this.permissions.has("reports.export");
  }
  get canWrite(): boolean {
    return this.permissions.has("admin.users.write");
  }
  users: User[] = [];
  get columns(): HisHopeDataTableColumn[] {
    this.i18n.locale();
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
        width: "96px",
        minWidth: 96,
        align: "center" as const,
        responsivePriority: 1,
        pinned: "right",
      },
    ];
  }
  tableRows: Record<string, unknown>[] = [];
  private readonly destroyRef = inject(DestroyRef);
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  private actionError: string | null = null;
  readonly resource = new HisHopeResourceStore<
    AdminPageQuery,
    AdminPageResult<User>
  >(
    (query) =>
      this.api.getUsersPage(query).pipe(
        catchError(() => {
          this.actionError = this.i18n.t(
            "admin.loadUsersFailed",
            "Failed to load users.",
          );
          return of<AdminPageResult<User>>({
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
    this.query,
    this.destroyRef,
  );
  bulkLoading = false;
  get loading(): boolean {
    return this.resource.loading() || this.bulkLoading;
  }
  get error(): string | null {
    return (
      this.actionError ||
      (this.resource.error()
        ? this.i18n.t("admin.loadUsersFailed", "Failed to load users.")
        : null)
    );
  }
  totalItems = 0;
  savedView: AdminSavedTableView = {};
  expandedRowKeys: string[] = [];
  constructor() {
    effect(() => {
      const result = this.resource.data();
      if (result) {
        this.totalItems = result.totalCount;
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
  get bulkActions(): HisHopeBulkAction[] {
    this.i18n.locale();
    return this.permissions.has("admin.users.write")
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

  ngOnInit(): void {
    this.loadServerView();
    this.loadUsers();
  }

  loadServerView(): void {
    this.api.getTableViews("users").subscribe((views) => {
      const view = views.find((item) => item.name === "default") ?? views[0];
      if (view) this.savedView = JSON.parse(view.payloadJson);
    });
  }
  saveView(event: { name: string; payload: unknown }): void {
    this.api.saveTableView("users", event.name, event.payload).subscribe();
  }
  resetView(event: { name: string }): void {
    this.savedView = {};
    this.api
      .deleteTableView("users", event.name)
      .subscribe({ error: () => this.loadServerView() });
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
  }

  loadUsers(query = this.query): void {
    this.query = query;
    this.actionError = null;
    this.resource.setQuery(query);
  }

  openCreateDialog(): void {
    if (!this.canWrite) return;
    this.dialog
      .open(UserEditDialogComponent, {
        width: "min(720px, calc(100vw - 32px))",
        maxWidth: "calc(100vw - 32px)",
        autoFocus: "first-tabbable",
        restoreFocus: true,
        data: null,
      })
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
          .open(UserEditDialogComponent, {
            width: "min(720px, calc(100vw - 32px))",
            maxWidth: "calc(100vw - 32px)",
            autoFocus: "first-tabbable",
            restoreFocus: true,
            data: detail,
          })
          .afterClosed()
          .subscribe((saved) => {
            if (saved) this.loadUsers(this.query);
          }),
      error: () =>
        this.snackBar.open(
          this.i18n.t("admin.loadUserFailed", "Failed to load user."),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
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
        this.snackBar.open(
          this.i18n.t("admin.updateUserFailed", "Failed to update user."),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
        ),
    });
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.loadUsers(query);
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.permissions.has("admin.users.write")) return;
    this.bulkLoading = true;
    this.api
      .bulkUsers(request)
      .pipe(
        finalize(() => (this.bulkLoading = false)),
        catchError(() => {
          this.actionError = this.i18n.t(
            "admin.updateUsersFailed",
            "Failed to update selected users.",
          );
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) this.loadUsers(this.query);
      });
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable("users", request).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `users-${new Date().toISOString().slice(0, 10)}.${request.format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }
}
