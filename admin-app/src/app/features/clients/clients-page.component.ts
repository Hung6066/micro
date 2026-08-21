import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  OnInit,
  effect,
  inject,
} from "@angular/core";
import { CommonModule } from "@angular/common";
import { HisHopeDialogService } from "@his-hope/frontend-foundation/ui";
import {
  AdminPageQuery,
  OidcClient,
} from "../../core/contracts/admin.contracts";
import { ClientsApiService } from "../../core/services/clients-api.service";
import { HisHopePageQuery } from "@his-hope/frontend-foundation";
import {
  HisHopeAuditFeedbackService,
  HisHopeBulkAction,
  HisHopeBulkActionRequest,
  HisHopeTableExportRequest,
} from "@his-hope/frontend-foundation";
import { HisHopePermissionService } from "@his-hope/frontend-foundation/auth";
import { AdminResourceTableController } from "../../core/services/admin-resource-table.controller";
import { downloadAdminTableExport } from "../../core/services/admin-query.util";
import {
  HisHopeConfirmDialogComponent,
  HisHopeDataTableCellDirective,
  HisHopeDataTableColumn,
  HisHopeDataTableComponent,
  HisHopeDataTableDetailDirective,
  HisHopePageHeaderComponent,
  HisHopePageLayoutComponent,
  HisHopeToastService,
  HisHopeToolbarComponent,
} from "@his-hope/frontend-foundation/ui";
import {
  HisHopeI18nService,
  HisHopeTranslatePipe,
} from "@his-hope/frontend-foundation/i18n";
import { ClientEditDialogComponent } from "./client-edit-dialog.component";
import { catchError } from "rxjs/operators";
import { of } from "rxjs";

import { HisHopeActionButtonComponent } from "@his-hope/frontend-foundation/ui";
@Component({
  selector: "app-clients-page",
  standalone: true,
  imports: [
    CommonModule,
    HisHopeTranslatePipe,
    HisHopeConfirmDialogComponent,
    HisHopeActionButtonComponent,
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
      />

      <hh-toolbar hhPageToolbar [label]="'admin.manageClients' | hhTranslate">
        <span hhToolbarTitle
          >{{ totalItems }} {{ "admin.clients" | hhTranslate }}</span
        >
        <hh-action-button
          *ngIf="canWrite"
          hh-toolbar-actions
          kind="primary"
          icon="add"
          [label]="'admin.newClient' | hhTranslate"
          (pressed)="openCreateDialog()"
        />
        <hh-action-button
          hh-toolbar-actions
          kind="secondary"
          icon="refresh"
          [label]="'admin.refresh' | hhTranslate"
          (pressed)="loadClients()"
        />
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
          <hh-action-button
            *ngIf="canWrite"
            kind="row"
            mode="icon-only"
            icon="vpn_key"
            [label]="'admin.rotateClientSecret' | hhTranslate"
            (pressed)="rotateSecret(clientFromRow(row))"
          />
          <hh-action-button
            *ngIf="canWrite"
            kind="danger"
            mode="icon-only"
            icon="delete"
            [label]="'admin.delete' | hhTranslate"
            (pressed)="deleteClient(clientFromRow(row))"
          />
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClientsPageComponent implements OnInit {
  private readonly api = inject(ClientsApiService);
  private readonly dialog = inject(HisHopeDialogService);
  private readonly toast = inject(HisHopeToastService);
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
  columns: HisHopeDataTableColumn[] = [];
  bulkActions: HisHopeBulkAction[] = [];
  tableRows: Record<string, unknown>[] = [];

  private createColumns(): HisHopeDataTableColumn[] {
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
  readonly table = new AdminResourceTableController<AdminPageQuery, OidcClient>(
    {
      destroyRef: this.destroyRef,
      i18n: this.i18n,
      initialQuery: { page: 1, pageSize: 20 },
      loader: (query) => this.api.getClientsPage(query),
      loadErrorMessageKey: "admin.loadClientsFailed",
      loadErrorFallback:
        "Failed to load clients. Make sure the API is running.",
      onStateChange: () => this.cdr.markForCheck(),
    },
  );
  get loading(): boolean {
    return this.table.loading;
  }
  get error(): string | null {
    return this.table.error;
  }
  clientPendingDelete: OidcClient | null = null;
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
      this.bulkActions = this.canWrite
        ? [
            {
              id: "delete",
              label: this.i18n.t("admin.deleteSelected"),
              tone: "danger",
            },
          ]
        : [];
      this.cdr.markForCheck();
    });
    effect(() => {
      const result = this.table.resource.data();
      if (result) {
        this.clients = result.items;
        this.tableRows = result.items.map((client) => ({
          id: client.id ?? client.clientId,
          clientId: client.clientId,
          displayName: client.displayName,
          clientType: client.clientType,
          redirectUris: (client.redirectUris || []).join(", "),
          entity: client,
        }));
        this.cdr.markForCheck();
      }
    });
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
          this.toast.error(
            this.i18n.t("admin.updateClientFailed", "Failed to update client"),
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
          this.toast.success(
            this.i18n.t("admin.clientUpdated", "Client updated"),
            { duration: 2000 },
          );
          this.loadClients();
        }
      });
  }

  ngOnInit(): void {
    this.table.loadServerView(() => this.api.getTableViews("clients"));
    this.loadClients();
  }

  loadServerView(): void {
    this.table.loadServerView(() => this.api.getTableViews("clients"));
  }

  saveView(event: { name: string; payload: unknown }): void {
    this.table.saveView(event, (name, payload) =>
      this.api.saveTableView("clients", name, payload),
    );
  }
  resetView(event: { name: string }): void {
    this.table.resetView(
      event,
      (name) => this.api.deleteTableView("clients", name),
      () => this.loadServerView(),
    );
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void {
    this.table.toggleRowExpand(event);
  }

  loadClients(query = this.query): void {
    this.table.load(query);
  }

  onQueryChange(query: HisHopePageQuery): void {
    this.loadClients(query);
  }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    if (!this.canWrite) return;
    this.table.runBulkAction(
      request,
      (bulkRequest) => this.api.bulkTable("clients", bulkRequest),
      "admin.updateClientsFailed",
      "Failed to update selected clients.",
    );
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable("clients", request).subscribe((blob) => {
      downloadAdminTableExport(blob, "clients", request.format);
    });
  }

  openCreateDialog(): void {
    if (!this.canWrite) return;
    const ref = this.dialog.open(ClientEditDialogComponent, {
      width: "min(720px, calc(100vw - 32px))",
      maxWidth: "calc(100vw - 32px)",
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
      this.toast.error(
        this.i18n.t(
          "admin.onlyConfidentialClients",
          "Only confidential clients can rotate a secret.",
        ),
        { duration: 3000 },
      );
      return;
    }
    this.api.rotateClientSecret(client.id).subscribe({
      next: (result) => {
        navigator.clipboard?.writeText(result.clientSecret);
        this.toast.success(
          this.i18n
            .t(
              "admin.newSecretCopied",
              "New secret copied for {{clientId}}. It will not be shown again.",
            )
            .replace("{{clientId}}", result.clientId),
          { duration: 6000 },
        );
      },
      error: () =>
        this.toast.error(
          this.i18n.t(
            "admin.rotateClientFailed",
            "Failed to rotate client secret.",
          ),
          { duration: 3000 },
        ),
    });
  }

  confirmDeleteClient(): void {
    if (!this.canWrite) return;
    const client = this.clientPendingDelete;
    this.clientPendingDelete = null;
    if (!client?.id) return;
    this.api.deleteClient(client.id).subscribe({
      next: () => {
        this.auditFeedback.report({
          action: "Delete",
          resource: "OIDC client",
          outcome: "success",
          message: "Client deleted.",
        });
        this.toast.success("Client deleted", { duration: 2000 });
        this.loadClients();
      },
      error: () => {
        this.auditFeedback.report({
          action: "Delete",
          resource: "OIDC client",
          outcome: "failure",
          message: "Failed to delete client.",
        });
        this.toast.error("Failed to delete client", { duration: 3000 });
      },
    });
  }
}
