import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatTableModule } from "@angular/material/table";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatCardModule } from "@angular/material/card";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import {
  AdminPageQuery,
  AdminPageResult,
  AdminSavedTableView,
  OidcClient,
} from "../../core/contracts/admin.contracts";
import { ClientsApiService } from "../../core/services/clients-api.service";
import { HisHopePageQuery } from "@his-hope/frontend-foundation";
import {
  HisHopeAuditFeedbackService,
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopeConfirmDialogComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableDetailDirective,
  HisHopeI18nService,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopePermissionService,
  HisHopeResourceState,
  HisHopeTableExportRequest,
  HisHopeToolbarComponent,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation";
import { ClientEditDialogComponent } from "./client-edit-dialog.component";
import { catchError, finalize } from "rxjs/operators";
import { of } from "rxjs";

@Component({
  selector: "app-clients-page",
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    HisHopeTranslatePipe,
    MatDialogModule,
    MatSnackBarModule,
    MatCardModule,
    MatProgressSpinnerModule,
    HisHopeConfirmDialogComponent,
    HisHopeDataTableCellDirective,
    HisHopeDataTableComponent,
    HisHopeDataTableDetailDirective,
    HisHopePageHeaderComponent,
    HisHopePageLayoutComponent,
    HisHopeToolbarComponent,
  ],
  template: `
    <hh-page-layout>
      <hh-page-header
        hhPageHeader
        [title]="'admin.oidcApplications' | hhTranslate"
        [subtitle]="'admin.clientsSubtitle' | hhTranslate"
      >
        <button
          *ngIf="canWrite"
          mat-raised-button
          color="primary"
          type="button"
          (click)="openCreateDialog()"
        >
          <mat-icon>add</mat-icon> {{ "admin.newClient" | hhTranslate }}
        </button>
      </hh-page-header>

      <hh-toolbar hhPageToolbar [label]="'admin.manageClients' | hhTranslate">
        <span hhToolbarTitle
          >{{ totalItems }} {{ "admin.clients" | hhTranslate }}</span
        >
        <button
          hh-toolbar-actions
          type="button"
          class="hh-icon-button"
          (click)="loadClients()"
          [attr.aria-label]="'admin.refresh' | hhTranslate"
          [attr.title]="'admin.refresh' | hhTranslate"
        >
          <span class="material-icons" aria-hidden="true">refresh</span>
        </button>
      </hh-toolbar>

      <hh-data-table
        [label]="'admin.oidcApplications' | hhTranslate"
        [loading]="loading"
        [error]="error ?? ''"
        [empty]="!loading && !error && clients.length === 0"
        [columns]="columns"
        [rows]="tableRows"
        [selection]="true"
        [inlineEdit]="canWrite"
        mode="server"
        [totalItems]="totalItems"
        [query]="query"
        [pageSize]="20"
        (queryChange)="onQueryChange($event)"
        [exportable]="canExport"
        [bulkActions]="bulkActions"
        [filterBuilder]="true"
        [virtualizeColumns]="true"
        [expandedRowKeys]="expandedRowKeys"
        viewStorageKey="admin.clients"
        viewName="default"
        [serverBackedView]="true"
        [savedView]="savedView"
        (rowExpandChange)="toggleRowExpand($event)"
        (viewSaveRequested)="saveView($event)"
        (viewResetRequested)="resetView($event)"
        (bulkActionRequested)="onBulkAction($event)"
        (exportRequested)="onExport($event)"
        [emptyMessage]="'admin.noClients' | hhTranslate"
        (rowEditSave)="saveInlineClient($event)"
        (retry)="loadClients()"
      >
        <ng-template hhDataTableCell="actions" let-row>
          <button
            *ngIf="canWrite"
            mat-icon-button
            type="button"
            (click)="rotateSecret(clientFromRow(row))"
            [attr.aria-label]="'admin.rotateClientSecret' | hhTranslate"
          >
            <mat-icon>vpn_key</mat-icon>
          </button>
          <button
            *ngIf="canWrite"
            mat-icon-button
            color="warn"
            type="button"
            (click)="deleteClient(clientFromRow(row))"
            [attr.aria-label]="'admin.delete' | hhTranslate"
          >
            <mat-icon>delete</mat-icon>
          </button>
        </ng-template>
        <ng-template hhDataTableDetail let-row>
          <div class="hh-data-table-detail">
            {{ row["clientType"] }} ·
            {{
              row["redirectUris"] ||
                i18n.t("admin.noRedirectUri", "No redirect URI")
            }}
          </div>
        </ng-template>
      </hh-data-table>
      <hh-confirm-dialog
        [open]="!!clientPendingDelete"
        [title]="
          i18n.t('admin.deleteClientConfirmTitle', 'Delete OIDC client?')
        "
        [message]="
          clientPendingDelete
            ? i18n.t(
                'admin.deleteClientConfirmMessage',
                'This will revoke the client registration and cannot be undone.'
              )
            : ''
        "
        [confirmLabel]="i18n.t('admin.deleteClient', 'Delete client')"
        (confirmed)="confirmDeleteClient()"
        (cancelled)="clientPendingDelete = null"
      />
    </hh-page-layout>
  `,
})
export class ClientsPageComponent implements OnInit {
  private readonly api = inject(ClientsApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auditFeedback = inject(HisHopeAuditFeedbackService);
  readonly i18n = inject(HisHopeI18nService);
  private readonly permissions = inject(HisHopePermissionService);
  private readonly cdr = inject(ChangeDetectorRef);
  get canExport(): boolean {
    return this.permissions.has("reports.export");
  }
  get canWrite(): boolean {
    return this.permissions.has("admin.clients.write");
  }

  clients: OidcClient[] = [];
  get columns(): HisHopeDataTableColumn[] {
    return [
      {
        key: "clientId",
        label: this.i18n.t("admin.clientId", "Client ID"),
        sortable: true,
        responsivePriority: 1,
        pinned: "left",
      },
      {
        key: "displayName",
        label: this.i18n.t("admin.displayName", "Display name"),
        sortable: true,
        editable: true,
        responsivePriority: 1,
      },
      {
        key: "clientType",
        label: this.i18n.t("admin.type", "Type"),
        editable: true,
        responsivePriority: 2,
      },
      {
        key: "redirectUris",
        label: this.i18n.t("admin.redirectUris", "Redirect URIs"),
        responsivePriority: 3,
      },
      {
        key: "actions",
        label: this.i18n.t("admin.actions", "Actions"),
        hideable: false,
        sortable: false,
        reorderable: false,
        width: "112px",
        minWidth: 112,
        align: "center" as const,
        responsivePriority: 1,
        pinned: "right",
      },
    ];
  }
  private readonly destroyRef = inject(DestroyRef);
  readonly resource = new HisHopeResourceState<AdminPageResult<OidcClient>>(
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
  clientPendingDelete: OidcClient | null = null;
  totalItems = 0;
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  savedView: AdminSavedTableView = {};
  expandedRowKeys: string[] = [];
  constructor() {
    effect(() => {
      const result = this.resource.data();
      if (result) {
        this.totalItems = result.totalCount;
        this.clients = result.items;
        this.cdr.markForCheck();
      }
    });
  }
  get bulkActions(): HisHopeBulkAction[] {
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

  get tableRows(): Record<string, unknown>[] {
    return this.clients.map((client) => ({
      id: client.id ?? client.clientId,
      clientId: client.clientId,
      displayName: client.displayName,
      clientType: client.clientType,
      redirectUris: (client.redirectUris || []).join(", "),
      entity: client,
    }));
  }

  clientFromRow(row: Record<string, unknown>): OidcClient {
    return row["entity"] as OidcClient;
  }

  saveInlineClient(row: Record<string, unknown>): void {
    if (!this.canWrite) return;
    const client = this.clientFromRow(row);
    if (!client?.id) return;
    this.api
      .updateClient(client.id, {
        displayName: String(row["displayName"] ?? client.displayName),
        clientType: String(row["clientType"] ?? client.clientType),
        concurrencyToken: client.concurrencyToken,
      })
      .pipe(
        catchError(() => {
          this.auditFeedback.report({
            action: "Update",
            resource: "OIDC client",
            outcome: "failure",
            message: this.i18n.t(
              "admin.updateClientFailed",
              "Failed to update client.",
            ),
          });
          this.snackBar.open(
            this.i18n.t("admin.updateClientFailed", "Failed to update client"),
            this.i18n.t("admin.close", "Close"),
            { duration: 3000 },
          );
          this.loadClients();
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) {
          this.auditFeedback.report({
            action: "Update",
            resource: "OIDC client",
            outcome: "success",
            message: this.i18n.t("admin.clientUpdated", "Client updated."),
          });
          this.snackBar.open(
            this.i18n.t("admin.clientUpdated", "Client updated"),
            this.i18n.t("admin.close", "Close"),
            { duration: 2000 },
          );
          this.loadClients();
        }
      });
  }

  ngOnInit(): void {
    this.loadServerView();
    this.loadClients();
  }

  loadServerView(): void {
    this.api.getTableViews("clients").subscribe((views) => {
      const view = views.find((item) => item.name === "default") ?? views[0];
      if (view) this.savedView = JSON.parse(view.payloadJson);
    });
  }

  saveView(event: { name: string; payload: unknown }): void {
    this.api.saveTableView("clients", event.name, event.payload).subscribe();
  }
  resetView(event: { name: string }): void {
    this.savedView = {};
    this.api
      .deleteTableView("clients", event.name)
      .subscribe({ error: () => this.loadServerView() });
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.expandedRowKeys = event.expanded
      ? [...this.expandedRowKeys, event.rowKey]
      : this.expandedRowKeys.filter((key) => key !== event.rowKey);
  }

  loadClients(query = this.query): void {
    this.query = query;
    this.actionError = null;
    this.resource.load(
      this.api.getClientsPage(query).pipe(
        catchError(() => {
          this.actionError = this.i18n.t(
            "admin.loadClientsFailed",
            "Failed to load clients. Make sure the API is running.",
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
    this.loadClients(query);
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.canWrite) return;
    this.bulkLoading = true;
    this.api
      .bulkTable("clients", request)
      .pipe(
        finalize(() => (this.bulkLoading = false)),
        catchError(() => {
          this.actionError = this.i18n.t(
            "admin.updateClientsFailed",
            "Failed to update selected clients.",
          );
          return of(null);
        }),
      )
      .subscribe((result) => {
        if (result) this.loadClients(this.query);
      });
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable("clients", request).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `clients-${new Date().toISOString().slice(0, 10)}.${request.format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  openCreateDialog(): void {
    if (!this.canWrite) return;
    const ref = this.dialog.open(ClientEditDialogComponent, {
      width: "min(720px, calc(100vw - 32px))",
      maxWidth: "calc(100vw - 32px)",
      autoFocus: "first-tabbable",
      restoreFocus: true,
    });
    ref.afterClosed().subscribe((result) => {
      if (result) this.loadClients();
    });
  }

  openEditDialog(client: OidcClient): void {
    if (!this.canWrite) return;
    const ref = this.dialog.open(ClientEditDialogComponent, {
      width: "min(720px, calc(100vw - 32px))",
      maxWidth: "calc(100vw - 32px)",
      autoFocus: "first-tabbable",
      restoreFocus: true,
      data: client,
    });
    ref.afterClosed().subscribe((result) => {
      if (result) this.loadClients();
    });
  }

  deleteClient(client: OidcClient): void {
    if (!this.canWrite) return;
    this.clientPendingDelete = client;
  }

  rotateSecret(client: OidcClient): void {
    if (!this.canWrite) return;
    if (!client.id || client.clientType?.toLowerCase() !== "confidential") {
      this.snackBar.open(
        this.i18n.t(
          "admin.onlyConfidentialClients",
          "Only confidential clients can rotate a secret.",
        ),
        this.i18n.t("admin.close", "Close"),
        { duration: 3000 },
      );
      return;
    }
    this.api.rotateClientSecret(client.id).subscribe({
      next: (result) => {
        navigator.clipboard?.writeText(result.clientSecret);
        this.snackBar.open(
          this.i18n
            .t(
              "admin.newSecretCopied",
              "New secret copied for {{clientId}}. It will not be shown again.",
            )
            .replace("{{clientId}}", result.clientId),
          this.i18n.t("admin.close", "Close"),
          { duration: 6000 },
        );
      },
      error: () =>
        this.snackBar.open(
          this.i18n.t(
            "admin.rotateClientFailed",
            "Failed to rotate client secret.",
          ),
          this.i18n.t("admin.close", "Close"),
          { duration: 3000 },
        ),
    });
  }

  confirmDeleteClient(): void {
    if (!this.canWrite) return;
    const client = this.clientPendingDelete;
    this.clientPendingDelete = null;
    if (!client?.id) return;
    this.api
      .deleteClient(client.id!)
      .pipe(
        catchError((err) => {
          this.auditFeedback.report({
            action: "Delete",
            resource: "OIDC client",
            outcome: "failure",
            message: "Failed to delete client.",
          });
          this.snackBar.open("Failed to delete client", "Close", {
            duration: 3000,
          });
          return of(undefined);
        }),
      )
      .subscribe(() => {
        this.auditFeedback.report({
          action: "Delete",
          resource: "OIDC client",
          outcome: "success",
          message: "Client deleted.",
        });
        this.snackBar.open("Client deleted", "Close", { duration: 2000 });
        this.loadClients();
      });
  }
}
