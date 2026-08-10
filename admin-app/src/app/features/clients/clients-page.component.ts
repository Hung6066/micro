import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AdminApiService, AdminPageQuery, OidcClient } from '../../core/services/admin-api.service';
import { HisHopePageQuery } from '@his-hope/frontend-foundation';
import { HisHopeAuditFeedbackService, HisHopeBulkAction, HisHopeBulkActionRequest, HisHopeConfirmDialogComponent, HisHopeDataTableCellDirective, HisHopeDataTableColumn, HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeTableExportRequest, HisHopeToolbarComponent } from '@his-hope/frontend-foundation';
import { ClientEditDialogComponent } from './client-edit-dialog.component';
import { catchError, finalize } from 'rxjs/operators';
import { of, Subscription } from 'rxjs';

@Component({
  selector: 'app-clients-page',
  standalone: true,
  imports: [
    CommonModule, MatTableModule, MatButtonModule, MatIconModule,
    MatDialogModule, MatSnackBarModule, MatCardModule, MatProgressSpinnerModule,
    HisHopeConfirmDialogComponent, HisHopeDataTableCellDirective, HisHopeDataTableComponent, HisHopeDataTableDetailDirective, HisHopePageHeaderComponent, HisHopePageLayoutComponent, HisHopeToolbarComponent,
  ],
  template: `
    <hh-page-layout>
    <hh-page-header hhPageHeader title="OIDC Clients" subtitle="Manage applications registered with the identity service">
      <button mat-raised-button color="primary" type="button" (click)="openCreateDialog()">
        <mat-icon>add</mat-icon> New Client
      </button>
    </hh-page-header>

    <hh-toolbar hhPageToolbar label="OIDC client controls">
      <span hhToolbarTitle>{{ totalItems }} clients</span>
      <button hh-toolbar-actions type="button" class="hh-icon-button" (click)="loadClients()" aria-label="Refresh clients" title="Refresh clients">
        <span class="material-icons" aria-hidden="true">refresh</span>
      </button>
    </hh-toolbar>

    <hh-data-table label="OIDC clients" [loading]="loading" [error]="error ?? ''"
                   [empty]="!loading && !error && clients.length === 0"
                   [columns]="columns" [rows]="tableRows" [selection]="true" [inlineEdit]="true" mode="server"
                   [totalItems]="totalItems" [query]="query" [pageSize]="20" (queryChange)="onQueryChange($event)"
                   [exportable]="true" [bulkActions]="bulkActions" [filterBuilder]="true" [virtualizeColumns]="true" [expandedRowKeys]="expandedRowKeys" viewStorageKey="admin.clients" viewName="default" [serverBackedView]="true" [savedView]="savedView"
                   (rowExpandChange)="toggleRowExpand($event)"
                   (viewSaveRequested)="saveView($event)" (viewResetRequested)="resetView($event)" (bulkActionRequested)="onBulkAction($event)" (exportRequested)="onExport($event)"
                   emptyMessage="No OIDC clients found." (rowEditSave)="saveInlineClient($event)"
                   (retry)="loadClients()">
      <ng-template hhDataTableCell="actions" let-row>
        <button mat-icon-button type="button" (click)="rotateSecret(clientFromRow(row))" aria-label="Rotate client secret">
          <mat-icon>vpn_key</mat-icon>
        </button>
        <button mat-icon-button color="warn" type="button" (click)="deleteClient(clientFromRow(row))" aria-label="Delete client">
          <mat-icon>delete</mat-icon>
        </button>
      </ng-template>
      <ng-template hhDataTableDetail let-row>
        <div class="hh-data-table-detail">{{ row['clientType'] }} · {{ row['redirectUris'] || 'No redirect URI' }}</div>
      </ng-template>
    </hh-data-table>
    <hh-confirm-dialog
      [open]="!!clientPendingDelete"
      title="Delete OIDC client?"
      [message]="clientPendingDelete ? 'This will revoke the client registration and cannot be undone.' : ''"
      confirmLabel="Delete client"
      (confirmed)="confirmDeleteClient()"
      (cancelled)="clientPendingDelete = null" />
    </hh-page-layout>
  `,
})
export class ClientsPageComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auditFeedback = inject(HisHopeAuditFeedbackService);

  clients: OidcClient[] = [];
  columns: HisHopeDataTableColumn[] = [
    { key: 'clientId', label: 'Client ID', sortable: true, responsivePriority: 1, pinned: 'left' },
    { key: 'displayName', label: 'Display Name', sortable: true, editable: true, responsivePriority: 1 },
    { key: 'clientType', label: 'Type', editable: true, responsivePriority: 2 },
    { key: 'redirectUris', label: 'Redirect URIs', responsivePriority: 3 },
    { key: 'actions', label: 'Actions', hideable: false, sortable: false, reorderable: false, width: '112px', minWidth: 112, align: 'center' as const, responsivePriority: 1, pinned: 'right' },
  ];
  loading = false;
  error: string | null = null;
  clientPendingDelete: OidcClient | null = null;
  totalItems = 0;
  query: AdminPageQuery = { page: 1, pageSize: 20 };
  savedView: any = null;
  expandedRowKeys: string[] = [];
  private pageRequest?: Subscription;
  readonly bulkActions: HisHopeBulkAction[] = [{ id: 'delete', label: 'Delete selected', tone: 'danger' }];

  get tableRows(): Record<string, unknown>[] {
    return this.clients.map(client => ({
      id: client.id ?? client.clientId,
      clientId: client.clientId,
      displayName: client.displayName,
      clientType: client.clientType,
      redirectUris: (client.redirectUris || []).join(', '),
      entity: client,
    }));
  }

  clientFromRow(row: Record<string, unknown>): OidcClient { return row['entity'] as OidcClient; }

  saveInlineClient(row: Record<string, unknown>): void {
    const client = this.clientFromRow(row);
    if (!client?.id) return;
    this.api.updateClient(client.id, {
      displayName: String(row['displayName'] ?? client.displayName),
      clientType: String(row['clientType'] ?? client.clientType),
      concurrencyToken: client.concurrencyToken,
    }).pipe(
      catchError(() => {
        this.auditFeedback.report({ action: 'Update', resource: 'OIDC client', outcome: 'failure', message: 'Failed to update client.' });
        this.snackBar.open('Failed to update client', 'Close', { duration: 3000 });
        this.loadClients();
        return of(null);
      }),
    ).subscribe(result => {
      if (result) {
        this.auditFeedback.report({ action: 'Update', resource: 'OIDC client', outcome: 'success', message: 'Client updated.' });
        this.snackBar.open('Client updated', 'Close', { duration: 2000 });
        this.loadClients();
      }
    });
  }

  ngOnInit(): void { this.loadServerView(); this.loadClients(); }

  loadServerView(): void {
    this.api.getTableViews('clients').subscribe(views => { const view = views.find(item => item.name === 'default') ?? views[0]; if (view) this.savedView = JSON.parse(view.payloadJson); });
  }

  saveView(event: { name: string; payload: unknown }): void { this.api.saveTableView('clients', event.name, event.payload).subscribe(); }
  resetView(event: { name: string }): void {
    this.savedView = null;
    this.api.deleteTableView('clients', event.name).subscribe({ error: () => this.loadServerView() });
  }
  toggleRowExpand(event: { rowKey: string; expanded: boolean }): void { this.expandedRowKeys = event.expanded ? [...this.expandedRowKeys, event.rowKey] : this.expandedRowKeys.filter(key => key !== event.rowKey); }

  loadClients(query = this.query): void {
    this.pageRequest?.unsubscribe();
    this.query = query;
    this.loading = true;
    this.error = null;
    this.pageRequest = this.api.getClientsPage(query).pipe(
      finalize(() => this.loading = false),
      catchError(err => {
        this.error = 'Failed to load clients. Make sure the API is running.';
        return of({ items: [], totalCount: 0, page: query.page, pageSize: query.pageSize, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
      }),
    ).subscribe(result => { this.totalItems = result.totalCount; this.clients = result.items; });
  }

  onQueryChange(query: HisHopePageQuery): void { this.loadClients(query); }

  onBulkAction(request: HisHopeBulkActionRequest): void {
    this.loading = true;
    this.api.bulkTable('clients', request).pipe(finalize(() => this.loading = false), catchError(() => { this.error = 'Failed to update selected clients.'; return of(null); }))
      .subscribe(result => { if (result) this.loadClients(this.query); });
  }

  onExport(request: HisHopeTableExportRequest): void {
    this.api.exportTable('clients', request).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `clients-${new Date().toISOString().slice(0, 10)}.${request.format}`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  openCreateDialog(): void {
    const ref = this.dialog.open(ClientEditDialogComponent, {
      width: 'min(720px, calc(100vw - 32px))',
      maxWidth: 'calc(100vw - 32px)',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadClients();
    });
  }

  openEditDialog(client: OidcClient): void {
    const ref = this.dialog.open(ClientEditDialogComponent, {
      width: 'min(720px, calc(100vw - 32px))',
      maxWidth: 'calc(100vw - 32px)',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      data: client,
    });
    ref.afterClosed().subscribe(result => {
      if (result) this.loadClients();
    });
  }

  deleteClient(client: OidcClient): void {
    this.clientPendingDelete = client;
  }

  rotateSecret(client: OidcClient): void {
    if (!client.id || client.clientType?.toLowerCase() !== 'confidential') {
      this.snackBar.open('Only confidential clients can rotate a secret.', 'Close', { duration: 3000 });
      return;
    }
    this.api.rotateClientSecret(client.id).subscribe({
      next: result => {
        navigator.clipboard?.writeText(result.clientSecret);
        this.snackBar.open(`New secret copied for ${result.clientId}. It will not be shown again.`, 'Close', { duration: 6000 });
      },
      error: () => this.snackBar.open('Failed to rotate client secret.', 'Close', { duration: 3000 }),
    });
  }

  confirmDeleteClient(): void {
    const client = this.clientPendingDelete;
    this.clientPendingDelete = null;
    if (!client?.id) return;
    this.api.deleteClient(client.id!).pipe(
      catchError(err => {
        this.auditFeedback.report({ action: 'Delete', resource: 'OIDC client', outcome: 'failure', message: 'Failed to delete client.' });
        this.snackBar.open('Failed to delete client', 'Close', { duration: 3000 });
        return of(undefined);
      }),
    ).subscribe(() => {
      this.auditFeedback.report({ action: 'Delete', resource: 'OIDC client', outcome: 'success', message: 'Client deleted.' });
      this.snackBar.open('Client deleted', 'Close', { duration: 2000 });
      this.loadClients();
    });
  }
}
